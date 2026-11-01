# 요술나무 프로젝트 규칙

## 이미지 생성
- **이미지 모델은 항상 kie.ai의 GPT Image 2.5 Sunburst**를 쓴다 (사용자 지정).
  - 글자→이미지 모델 ID는 확인 필요(추정: `gpt-image-2-5-sunburst-text-to-image`).
    이미지→이미지 ID는 `gpt-image-2-5-sunburst-image-to-image` (docs.kie.ai 확인).
  - 실제로 성공한 ID를 `art/manifest.json`의 `kie.model`에 기록해 둔다.
- 다른 모델(nano-banana, Higgsfield 등)은 사용자가 명시적으로 요청할 때만 쓴다.
- **이미지는 사용자 승인 필수**: 프롬프트 승인 → 생성 → 사용자가 후보를 고른 뒤에만
  `tools/kie/kie_art.py approve` 로 `Assets/Resources/Art/`에 넣는다.
- `KIE_API_KEY`는 환경 변수로만 다루고, 값은 절대 출력·커밋하지 않는다.

## 소통
- 사용자에게는 한국어로 답한다.
