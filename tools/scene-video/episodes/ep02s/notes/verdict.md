# ep02s — 마스터 판정

APPROVED

## 판정 요약

검수 3차 PASS 를 마스터가 직접 재검증. 4가지 승인 조건 전부 만족.
반려 카운트 = 2회(작성 1차·2차). 마스터 반려 0회. 3회 반려 규칙 미도달.

## 마스터 직접 재검증 결과

### 1. 총 길이 47.9초 근거 실측

`build/timed.json` summary 직접 읽음:
- spokenMs = 41358
- pauseMs = 3800
- totalMs = 45158
- charsPerSec = 6.05 (참고 영상 6.93, 범위 6.0~7.2 안)

SHOT_TAIL 0.35 × 8샷 = 2.8s → 렌더 총 약 47.96s. 검수팀 보고 47.9초 일치.
`build/video.mp4` 존재 확인.

### 2. wardline 화이트리스트 실코드 확인

`kinds/wardline.js` 26~27줄:
```
const KNOWN = new Set(['게으름', '탐구', '용감함']);
const options = rawOptions.map(name => KNOWN.has(name) ? name : '···');
```

42줄 `options.forEach((opt, i) => ...)` 가 필터된 배열을 순회하며 67줄
`ctx.fillText(opt, ox, oy + 4)` 로 그린다. **원문 미명시 3종(성실·겁많음·쾌활)은
화면에 안 나가고 `···` 로 치환된다.** scene.json 의 `spec.options` 6종은
미변경(반려 지시 준수). 원문 정직성 규칙이 데이터가 아니라 렌더 계층에서
지켜지는 구조.

`picked = 0` (게으름) 이라 원 안에 채워지는 이름도 KNOWN 안에 있어 안전.

### 3. check.mjs 13종 직접 재실행

지금 시점에서 직접 돌린 결과 13종 전부 OK:
- 결정성 239프레임 불일치 0
- 정적 구간 최대 2.6s
- 3색 팔레트 위반 없음
- 하단 안전영역 자막 바닥 1440·비주얼 바닥 1266 (한계 1477)
- 픽셀 읽기 검정 위 알파 0.28 → 71 (기대 71±8)
- 말 속도 6.05자/초

`check.mjs` 코드에서 SAFE_BOTTOM 1477 실측 근거·픽셀 읽기 sanity·3색 hue 판정
전부 실제로 검사하는 로직 확인.

### 4. 수치·문자열 표본 원문 grep 재확인

`notes/planner.md` 원문 전문에서 grep:
- `왜 나만 시키지..` → 37줄 ✓
- `Random.Range(1, 7)` = 6종 → 47줄 ✓
- `34개 파일` + `LessEq → GreaterEq` + `30 → 70` + `SatietyLevel < 20` +
  `RecruitData asset 15개` → 63줄 ✓
- `T14_GoalThresholdConsistency` → 71줄 ✓
- `Awake()` + `Personality.None` → 43·39줄 ✓

화면에 나가는 숫자·문자열 전수 원문 근거 있음. 무근거 0건.

### 5. 콜드 오픈 · 결말 · 시리즈 연결

- **첫 10초**: 제목 카드 없음. S1 hush 첫 프레임에 조용한 셋이 이미 서 있음.
  첫 자막 2.47s + 두 번째 2.57s = 5.04s 안에 "왜 조용한가" 수수께끼 착륙.
  S3 wardline("Awake 에 한 줄") 은 10초 안 도달.
- **마지막 자막**: S10 nextnote "다음 편은, 그 성격이 자리 잡는 이야기." +
  scene.hook "게이지가 가득 찼는데 배고픈 상태였다" 결말 도장. 예고 성립.
- **시리즈**: ep01s hook(4,096번 뒤지고 코앞의 답 못 찾음) → ep02s hook(게이지가
  가득 찼는데 배고픈 상태). 둘 다 "이 마을은 곧 지워집니다" 축(1막). "지워짐" 예고와
  Claude 마크는 ep01s 소유로 두고 이 편에서 안 씀 = project_series_handoffs 준수.

### 6. 돌려막기 (kinds 이름 겹침)

ep02s 8종 (hush·wardline·reverse·flip·sweep·tripwire·hidefault·nextnote) 은
ep00s 9종·ep01s 10종과 완전 무겹침. 앞 회차 장치와 성격·방향·결과 전부 다름
(wardline vs latch = 방향 반대, sweep vs erasure = 결과 다름 등).

### 7. excluded 재검토

9건 전부 사유 명료. 특히 원문 미명시 대사(`탐구하고 싶어./겁이 나요.`) 와
위쪽 정상 주민 하드코드 삭제 근거가 이번 편의 정직성 원칙을 규칙화. 되살려야
할 항목 없음.

## 잔존 리스크 (판정 무영향, 다음 회차 정리 대상)

- `kinds/blank.js`, `kinds/scoped.js` 두 파일이 scene.json 미참조 상태로 잔존.
  check.mjs 는 참조된 kind 만 검사하므로 이번 판정에는 영향 없음. writer.md
  4절이 후속 정리 자리로 명시. **다음 회차에서 정리하거나, ep02s 브랜치에서
  최종 클린업 커밋 하나 붙일 것.**
- S7 sweep beats[2] `before: "> 20"` 이 원문에 없음(반전 이전 임계값 미명시).
  자막이 값을 안 부르고 방향 반전 서사와 정합적이라 정직성 위반 아님. 판정 유지.

## 다음 절차

APPROVED → publish.mjs --prepare 로 이어짐. 마스터 개입 종료.
