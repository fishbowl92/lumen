# LUMEN Unity 프로젝트 규칙

3D 빛 경로 퍼즐 LUMEN의 Unity 6.3 LTS (6000.3.28f1, URP) 프로젝트. 구현 지시서: `../implementation_plan_core.md`

## 구조

- `Assets/Lumen/Core/` — 규칙 코어. **순수 C#, UnityEngine 금지** (asmdef `noEngineReferences: true`). 게임과 솔버가 함께 쓴다
- `Assets/Lumen/Core.Tests/` — EditMode 테스트. 규칙을 바꾸면 테스트부터 고친다
- `Assets/Lumen/Game/` — Unity 화면·입력·카메라
- `Assets/Lumen/Resources/Levels/` — 레벨 JSON (JsonUtility 호환 스키마, 파일 이름순으로 레벨 순서가 정해진다)
- 규칙 참조 구현: `../reference/tracer.py`

## 지켜야 할 것

- 입력은 **Input System 패키지만** 쓴다 (`Mouse.current`, `Keyboard.current`). `UnityEngine.Input`은 런타임 오류
- 씬 파일(.unity)과 프리팹 YAML을 직접 편집하지 않는다. 화면 구성은 `LumenBootstrap`이 런타임에 만든다
- `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 건드리지 않는다
- 새 스크립트에는 .meta 파일을 직접 만들지 않는다 (Unity가 생성)
- 규칙은 기획서 규칙 명세와 지시서의 반사 표가 기준이다. 표에 없는 경우를 만나면 임의로 정하지 말고 질문한다
- 웹 빌드 대상: WebGL. 스레드·파일 시스템·리플렉션에 기대는 코드 금지

## 커밋

- 한국어. 첫 줄 50자 안쪽 요약, 한 줄 비우고 본문에 이유·변경 내용·확인 방법·남은 한계
- 테스트가 통과한 상태에서만 커밋
- AI가 작성을 도운 커밋은 본문 끝에 밝힌다
