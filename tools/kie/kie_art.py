#!/usr/bin/env python3
"""
요술나무 게임 아트 파이프라인 (kie.ai)

승인 흐름 (사용자 승인 없이는 게임에 들어가지 않음):
  prompt_draft ──approve-prompt──▶ prompt_approved ──generate──▶ awaiting_approval
        ▲                                                         │
        └───────────── reject (프롬프트 수정 후 재승인) ◀──────────┤
                                                                  └─approve──▶ approved
                                                                    (Assets/Resources/Art/{id}.png 로 복사)

사용법:
  export KIE_API_KEY=...            # 키는 환경변수로만. 파일/채팅에 붙여넣지 말 것.
  python3 tools/kie/kie_art.py list
  python3 tools/kie/kie_art.py approve-prompt tree_stage_0 tree_stage_1   # 또는 all
  python3 tools/kie/kie_art.py generate tree_stage_0 --variants 2
  python3 tools/kie/kie_art.py generate tree_stage_0 --model <모델ID> --set resolution=1K   # 이번만 다른 모델
  python3 tools/kie/kie_art.py approve tree_stage_0 2                     # 2번 후보 채택
  python3 tools/kie/kie_art.py reject tree_stage_0
  python3 tools/kie/kie_art.py bounds                                     # art_bounds.txt 다시 계산

API (docs.kie.ai, 2026-11 기준 — 변경 시 아래 상수만 수정):
  POST {BASE}/jobs/createTask      body: {"model", "input": {...}}  → data.taskId
  GET  {BASE}/jobs/recordInfo?taskId=...  → data.state (waiting|queuing|generating|success|fail),
                                            data.resultJson (문자열 JSON, resultUrls 배열)
"""
import argparse
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

BASE = os.environ.get("KIE_API_BASE", "https://api.kie.ai/api/v1")
ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "art" / "manifest.json"
PENDING = ROOT / "art" / "pending"
REJECTED = ROOT / "art" / "rejected"
APPROVED = ROOT / "Assets" / "Resources" / "Art"

BOUNDS = APPROVED / "art_bounds.txt"   # 게임이 읽는 '보이는 영역' (투명 여백 제외)
ALPHA_CUTOFF = 40                      # 이 값 이하의 알파는 여백으로 본다

POLL_INTERVAL = 4
POLL_TIMEOUT = 300


def load():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def save(m):
    MANIFEST.write_text(json.dumps(m, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def find(m, asset_id):
    for a in m["assets"]:
        if a["id"] == asset_id:
            return a
    sys.exit(f"알 수 없는 asset id: {asset_id}")


def api(method, path, body=None):
    key = os.environ.get("KIE_API_KEY")
    if not key:
        sys.exit("KIE_API_KEY 환경변수가 없습니다. 클라우드 환경 설정(또는 로컬 셸)에 추가하세요.")
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method, headers={
        "Authorization": f"Bearer {key}",
        "Content-Type": "application/json",
    })
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            payload = json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        sys.exit(f"HTTP {e.code}: {e.read().decode(errors='replace')[:500]}")
    except urllib.error.URLError as e:
        sys.exit(f"kie.ai 접속 실패 ({e.reason}). 네트워크 허용 도메인에 api.kie.ai 가 있는지 확인하세요.")
    if payload.get("code") not in (200, None):
        sys.exit(f"kie.ai 오류 code={payload.get('code')} msg={payload.get('msg')}")
    return payload.get("data") or {}


def create_task(m, asset, model=None, extra=None):
    """model 을 주면 이번 요청에만 그 모델을 쓰고, manifest 의 input_defaults 는 쓰지 않는다
    (모델마다 받는 input 필드가 다르기 때문). extra 는 --set 으로 받은 input 필드."""
    kie = m.get("kie", {})
    inp = {} if model else dict(kie.get("input_defaults", {}))
    inp["prompt"] = f"{asset['prompt']}. Style: {m.get('style', '')}".strip()
    if asset.get("aspect_ratio"):
        inp["aspect_ratio"] = asset["aspect_ratio"]
    inp.update(extra or {})
    model = model or kie.get("model", "nano-banana-2")
    data = api("POST", "/jobs/createTask", {"model": model, "input": inp})
    task_id = data.get("taskId")
    if not task_id:
        sys.exit(f"taskId 없음: {data}")
    return task_id


def wait_task(task_id):
    start = time.time()
    while time.time() - start < POLL_TIMEOUT:
        data = api("GET", f"/jobs/recordInfo?taskId={task_id}")
        state = data.get("state")
        if state == "success":
            result = data.get("resultJson") or "{}"
            if isinstance(result, str):
                result = json.loads(result)
            urls = result.get("resultUrls") or []
            if not urls:
                sys.exit(f"결과 URL 없음: {data}")
            return urls
        if state == "fail":
            sys.exit(f"생성 실패: {data.get('failMsg') or data}")
        print(f"  … {state}")
        time.sleep(POLL_INTERVAL)
    sys.exit(f"시간 초과 (taskId={task_id}). 나중에 recordInfo 로 확인하세요.")


def download(url, dest):
    with urllib.request.urlopen(url, timeout=120) as r, open(dest, "wb") as f:
        shutil.copyfileobj(r, f)


def write_bounds():
    """승인된 PNG마다 불투명 영역을 0~1 비율로 기록한다 (Unity 텍스처 좌표: 왼쪽 아래가 원점).
    게임(ArtLibrary)이 이 값으로 스프라이트의 투명 여백을 잘라내서, 단계마다 여백이 달라도
    나무 밑동이 같은 땅 위치에 오게 한다. Pillow 가 없으면 건너뛴다(게임은 여백 포함 전체를 쓴다)."""
    try:
        from PIL import Image
    except ImportError:
        print("  (Pillow 없음: art_bounds.txt 갱신 생략 — pip install pillow)")
        return
    lines = ["# id x_min y_min x_max y_max  (0~1, 왼쪽 아래 원점) — tools/kie/kie_art.py 가 자동 생성"]
    for png in sorted(APPROVED.glob("*.png")):
        with Image.open(png) as im:
            w, h = im.size
            box = None
            if "A" in im.getbands():
                box = im.getchannel("A").point(lambda v: 255 if v > ALPHA_CUTOFF else 0).getbbox()
            l, t, r, b = box or (0, 0, w, h)
        lines.append(f"{png.stem} {l / w:.4f} {1 - b / h:.4f} {r / w:.4f} {1 - t / h:.4f}")
    BOUNDS.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"  보이는 영역 갱신: {BOUNDS.relative_to(ROOT)}")


def candidates(asset_id):
    return sorted(PENDING.glob(f"{asset_id}__v*.png"))


# ───────────────────────── commands ─────────────────────────

def cmd_list(_):
    m = load()
    print(f"{'ID':<20} {'상태':<18} 이름")
    for a in m["assets"]:
        extra = f"  (후보 {len(candidates(a['id']))}장)" if a["status"] == "awaiting_approval" else ""
        print(f"{a['id']:<20} {a['status']:<18} {a['name_ko']}{extra}")


def cmd_approve_prompt(args):
    m = load()
    ids = [a["id"] for a in m["assets"]] if args.ids == ["all"] else args.ids
    for i in ids:
        a = find(m, i)
        if a["status"] in ("prompt_draft", "rejected"):
            a["status"] = "prompt_approved"
            print(f"프롬프트 승인: {i}")
        else:
            print(f"건너뜀 ({a['status']}): {i}")
    save(m)


def cmd_generate(args):
    m = load()
    a = find(m, args.id)
    if a["status"] not in ("prompt_approved", "awaiting_approval"):
        sys.exit(f"{args.id} 는 프롬프트 승인이 필요합니다 (현재: {a['status']}).")
    PENDING.mkdir(parents=True, exist_ok=True)
    n0 = len(candidates(args.id))
    extra = {}
    for kv in args.set or []:
        k, sep, v = kv.partition("=")
        if not sep:
            sys.exit(f"--set 형식은 KEY=VALUE 입니다: {kv}")
        extra[k] = v
    if args.model:
        print(f"이번 생성에만 모델 사용: {args.model}")
    for v in range(args.variants):
        print(f"[{args.id}] 생성 요청 {v + 1}/{args.variants}")
        urls = wait_task(create_task(m, a, args.model, extra))
        for url in urls:
            n0 += 1
            dest = PENDING / f"{args.id}__v{n0}.png"
            download(url, dest)
            print(f"  저장: {dest.relative_to(ROOT)}")
    a["status"] = "awaiting_approval"
    save(m)
    print("→ 이미지를 확인하고 approve / reject 해 주세요.")


def cmd_approve(args):
    m = load()
    a = find(m, args.id)
    files = candidates(args.id)
    pick = PENDING / f"{args.id}__v{args.variant}.png"
    if pick not in files:
        sys.exit(f"후보가 없습니다: {pick.name} (있는 후보: {[f.name for f in files]})")
    APPROVED.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(pick, APPROVED / f"{args.id}.png")
    REJECTED.mkdir(parents=True, exist_ok=True)
    for f in files:
        shutil.move(str(f), REJECTED / f.name) if f != pick else f.unlink()
    a["status"] = "approved"
    a["approved_from"] = pick.name
    save(m)
    print(f"승인 완료 → Assets/Resources/Art/{args.id}.png")
    write_bounds()


def cmd_reject(args):
    m = load()
    a = find(m, args.id)
    REJECTED.mkdir(parents=True, exist_ok=True)
    for f in candidates(args.id):
        shutil.move(str(f), REJECTED / f.name)
    a["status"] = "rejected"
    save(m)
    print(f"거절됨: {args.id}. 프롬프트를 수정한 뒤 approve-prompt 로 다시 승인하세요.")


def main():
    p = argparse.ArgumentParser(description="요술나무 kie.ai 아트 파이프라인")
    sub = p.add_subparsers(dest="cmd", required=True)
    sub.add_parser("list").set_defaults(fn=cmd_list)
    s = sub.add_parser("approve-prompt"); s.add_argument("ids", nargs="+"); s.set_defaults(fn=cmd_approve_prompt)
    s = sub.add_parser("generate"); s.add_argument("id"); s.add_argument("--variants", type=int, default=2)
    s.add_argument("--model", help="이번 생성에만 쓸 kie 모델 ID (manifest 기본값은 그대로)")
    s.add_argument("--set", action="append", metavar="KEY=VALUE", help="input 필드 추가/덮어쓰기 (여러 번 가능)")
    s.set_defaults(fn=cmd_generate)
    s = sub.add_parser("approve"); s.add_argument("id"); s.add_argument("variant", type=int); s.set_defaults(fn=cmd_approve)
    s = sub.add_parser("reject"); s.add_argument("id"); s.set_defaults(fn=cmd_reject)
    sub.add_parser("bounds", help="승인 이미지의 보이는 영역(art_bounds.txt) 다시 계산").set_defaults(fn=lambda _: write_bounds())
    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
