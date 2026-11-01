# 요술나무 (Magic Tree)

Unity로 만드는 방치형 클리커 게임 프로토타입입니다.

## 게임 개요
- 나무를 **터치**해서 마나 이슬을 모읍니다.
- 누적 마나에 따라 나무가 **씨앗 → 새싹 → 어린 나무 → 큰 나무 → 요술나무**로 자랍니다.
- 어린 나무부터 **요술 열매**가 열리고, 열매를 터치하면 터치 10번만큼의 보너스를 받습니다.
- 업그레이드: **물뿌리개**(터치당 마나 +1) / **요정 친구**(초당 자동 마나 +0.5) / **햇살**(열매가 더 빨리 열림)
- 자동 저장(PlayerPrefs)과 오프라인 보상(최대 8시간)이 있습니다.

밸런스 수치는 `Assets/Scripts/Core/GameConfig.cs` 한 곳에 모여 있습니다.
화면 배치 수치(땅 높이, 단계별 나무 높이, 정보 패널 높이)는 `Assets/Scripts/Core/MagicTreeGame.cs` 위쪽 상수에 있습니다.

## 실행 방법
1. Unity Hub → **Add project from disk** → 이 폴더를 선택합니다.
   - `ProjectSettings/ProjectVersion.txt`에는 Unity 6(`6000.0.x`)이 적혀 있습니다. 설치된 버전이 다르면 Hub에서 그 버전으로 열면 됩니다(확인 필요: 테스트한 에디터 버전은 아직 없음).
2. 아무 씬에서나 **Play**를 누르면 `GameBootstrap`이 카메라와 UI를 코드로 직접 만듭니다. 씬 설정은 필요 없습니다.
3. Game 뷰 해상도는 세로형(1080×1920)을 권장합니다.

승인된 이미지가 아직 없으면 색깔 도형(플레이스홀더)으로 표시됩니다.

## 이미지 파이프라인 (승인 필수)
`art/manifest.json`에 에셋 목록과 프롬프트가 있습니다. **사용자 승인 없이는 게임에 들어가지 않습니다.**

```
prompt_draft → (프롬프트 승인) → prompt_approved → (생성) → awaiting_approval → (이미지 승인) → approved
```

```bash
export KIE_API_KEY=...                                   # 환경변수로만 설정
python3 tools/kie/kie_art.py list                        # 상태 보기
python3 tools/kie/kie_art.py approve-prompt tree_stage_0 # 프롬프트 승인
python3 tools/kie/kie_art.py generate tree_stage_0       # 후보 2장 생성 → art/pending/
python3 tools/kie/kie_art.py approve tree_stage_0 1      # 1번 후보 채택 → Assets/Resources/Art/
python3 tools/kie/kie_art.py reject tree_stage_0         # 전부 거절
```

- 승인된 이미지는 `Assets/Resources/Art/{id}.png`에 들어가고, 게임이 자동으로 읽습니다.
- 승인할 때 `Assets/Resources/Art/art_bounds.txt`에 각 그림의 **보이는 영역**(투명 여백 제외)이 자동 기록됩니다. 게임은 이 값으로 여백을 잘라서, 단계마다 여백이 달라도 나무 밑동이 같은 땅 위치에 오게 합니다. 직접 PNG를 바꿨다면 `python3 tools/kie/kie_art.py bounds`로 다시 계산하세요(Pillow 필요).
- `Assets/Editor/ArtImportSettings.cs`가 Art 폴더 이미지를 Sprite·NPOT 유지·최대 4096으로 임포트합니다. 프로젝트를 이 파일 추가 전에 열었다면 Art 폴더를 **Reimport** 하세요.
- kie.ai 모델 이름과 입력 필드는 `art/manifest.json`의 `kie` 항목에서 바꿉니다(기본값: GPT Image 2.5 Sunburst `gpt-image-2-5-sunburst-text-to-image`, 1K·투명 배경).
- 클라우드 세션에서 쓰려면 환경 설정의 허용 도메인에 `api.kie.ai`(그리고 결과 이미지를 내려받는 호스트)를 추가하고, 환경 변수에 `KIE_API_KEY`를 넣어야 합니다.

## 폴더 구조
```
Assets/Scripts/Core   게임 로직, 저장, 밸런스, 부트스트랩
Assets/Scripts/UI     코드로 UI를 만드는 도우미
Assets/Scripts/Art    승인 이미지 / 플레이스홀더 로더
Assets/Editor         Art 폴더 임포트 설정
Assets/Resources/Art  승인된 이미지만 들어가는 곳
art/manifest.json     에셋 목록, 프롬프트, 승인 상태
art/pending           생성된 후보(커밋하지 않음)
tools/kie/kie_art.py  kie.ai 생성 도구
```
