# 자막 안전 영역 가이드

자막이 콘텐츠를 가리지 않도록 하는 레이아웃 가이드.

## 화면 레이아웃 (1920x1080)

```
┌─────────────────────────────────────┐ 0px
│                                     │
│                                     │
│      콘텐츠 안전 영역 (850px)        │
│                                     │
│        모든 콘텐츠는 여기에          │
│                                     │
│                                     │
├─────────────────────────────────────┤ 850px
│                                     │
│         자막 영역 (230px)            │
│    ┌─────────────────────────┐      │
│    │  ▓▓ 자막 텍스트 ▓▓▓▓▓  │      │ bottom-32 (128px)
│    └─────────────────────────┘      │
│                                     │
└─────────────────────────────────────┘ 1080px
```

## 핵심 치수

| 영역 | 높이 | 용도 |
|------|------|------|
| 콘텐츠 안전 영역 | 850px | 모든 시각적 콘텐츠 |
| 자막 영역 | 230px | 자막 전용 공간 |
| 자막 위치 | bottom-32 (128px) | 자막 박스 배치 |

---

## SafeContentArea 컴포넌트

**모든 세그먼트에서 권장하는 패턴:**

```tsx
const SafeContentArea: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  return (
    <AbsoluteFill style={{ backgroundColor: '#000000' }}>
      <div
        className="absolute inset-0 flex flex-col items-center justify-center px-20"
        style={{ paddingBottom: 230 }}
      >
        {children}
      </div>
    </AbsoluteFill>
  );
};
```

**사용법:**
```tsx
const MySegment: React.FC = () => {
  return (
    <SafeContentArea>
      <h1 className="text-8xl font-black" style={{ color: '#FFFFFF' }}>
        여기에 콘텐츠
      </h1>
    </SafeContentArea>
  );
};
```

---

## 레이아웃 패턴

### 패턴 1: 중앙 정렬 콘텐츠 (단일 phase)

```tsx
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
  <div
    className="absolute inset-0 flex flex-col items-center justify-center px-20"
    style={{ paddingBottom: 230 }}
  >
    <p className="text-8xl font-black" style={{ color: '#FFFFFF' }}>
      단일 메시지
    </p>
  </div>
</AbsoluteFill>
```

### 패턴 2: 다중 Phase (phase 전환이 있을 때 — 필수)

**각 phase를 독립적인 absolute 컨테이너로 분리해야 중앙 정렬이 유지된다.**
같은 flex column에 여러 phase를 넣으면 opacity 0인 요소도 공간을 차지하여 콘텐츠가 위로 밀린다.

```tsx
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
  {/* Phase 1: 독립 레이어 */}
  <div
    className="absolute inset-0 flex flex-col items-center justify-center px-20"
    style={{ paddingBottom: 230, opacity: phase1Opacity }}
  >
    <p className="text-7xl font-black" style={{ color: '#FFFFFF' }}>
      Phase 1 콘텐츠
    </p>
  </div>

  {/* Phase 2: 독립 레이어 */}
  <div
    className="absolute inset-0 flex flex-col items-center justify-center px-20"
    style={{ paddingBottom: 230, opacity: phase2Opacity }}
  >
    <p className="text-9xl font-black" style={{ color: '#00FF88' }}>
      Phase 2 콘텐츠
    </p>
  </div>
</AbsoluteFill>
```

### 패턴 3: 인포그래픽 점진적 빌드업

인포그래픽에서 나중에 등장하는 항목은 **조건부 렌더링**으로 DOM에서 제외하여 레이아웃 공간을 차지하지 않도록 한다:

```tsx
<div className="w-full max-w-5xl flex flex-col gap-5">
  <ComparisonBar label="비용" ... />
  {/* 시간 항목: 등장 전까지 DOM에서 제외 */}
  {frame >= timeStartFrame - 15 && (
    <>
      <ComparisonBar label="시간" ... />
    </>
  )}
</div>
```

### 패턴 4: 좌측 정렬 (에디토리얼 스타일)

```tsx
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
  <div
    className="absolute inset-0 flex flex-col items-start justify-center pl-32 px-20"
    style={{ paddingBottom: 230 }}
  >
    <p className="text-7xl font-black" style={{ color: '#FFFFFF' }}>대담한</p>
    <p className="text-7xl font-black -mt-2" style={{ color: '#00FF88' }}>선언</p>
  </div>
</AbsoluteFill>
```

---

## 절대 금지 사항

```tsx
// ❌ 절대 하단 포지셔닝 사용 금지
<div className="absolute bottom-20">콘텐츠</div>

// ❌ 여러 phase를 하나의 flex column에 넣지 않기
<div className="flex flex-col items-center justify-center">
  <div style={{ opacity: phase1 }}>Phase 1</div>
  <div style={{ opacity: phase2 }}>Phase 2</div>  // ← opacity 0이어도 공간 차지!
</div>

// ❌ 보이지 않는 요소를 레이아웃 흐름에 남기지 않기
// opacity: 0 인 요소도 flex/flow 안에서 공간을 차지함
// → 조건부 렌더링 또는 별도 absolute 레이어 사용
```

---

## 자막 컴포넌트 (표준 스타일)

**기본 자막 스타일: 배경 없음 + 흰색 굵은 텍스트 + 그림자**

배경 박스 없이 흰색 텍스트만 표시한다. 텍스트 그림자로 검정 배경 위 가독성 확보.

```
  유튜브를 시작하기 전에
```

### PodcastCaption (권장 구현)

`PLAYBACK_RATE`를 config에서 import하여 오디오↔프레임 시간 변환.

```tsx
import { PLAYBACK_RATE } from "./config";

export const PodcastCaption: React.FC<{ lines: CaptionLine[] }> = ({ lines }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const audioTime = (frame / fps) * PLAYBACK_RATE;

  const currentLine = lines.find(
    (line) => audioTime >= line.start && audioTime < line.end,
  );
  if (!currentLine) return null;

  return (
    <div className="absolute bottom-24 left-0 right-0 flex justify-center px-8 z-50">
      <p
        className="font-black text-center whitespace-nowrap"
        style={{
          fontSize: 56,
          color: "#FFFFFF",
          textShadow: "0 2px 8px rgba(0,0,0,0.6)",
        }}
      >
        {currentLine.text}
      </p>
    </div>
  );
};
```

### 자막 스타일 명세

| 속성 | 값 | 설명 |
|------|-----|------|
| 배경 | 없음 | 배경 박스 사용하지 않음 |
| 텍스트 색상 | `#FFFFFF` | 순수 흰색 |
| 폰트 크기 | `56px` | 선명하게 읽히는 큰 크기 |
| 폰트 두께 | `900` (font-black) | 최대 굵기 |
| 위치 | `bottom-24` (96px from bottom) | 화면 하단 고정 |
| 텍스트 그림자 | `0 2px 8px rgba(0,0,0,0.6)` | 검정 배경 위 깊이감 |

### 자막 금지 사항

```tsx
// ❌ 배경 박스
backgroundColor: 'rgba(0, 0, 0, 0.85)'

// ❌ 보더/테두리
border: '2px solid ...'

// ❌ 그라데이션
background: 'linear-gradient(...)'

// ❌ 카라오케/워드별 하이라이팅 (단어별 색 변경 전부 금지)
WebkitBackgroundClip: "text"
// ❌ 단어별 강조색 (active 단어만 #00FF88) — 금지, 항상 흰색 전체 라인
{line.words.map((w) => <span style={{ color: active ? ACCENT : "#FFF" }}>{w.text}</span>)}
// ✅ 반드시 라인 전체를 흰색 텍스트로: {currentLine.text}

// ❌ 텍스트 외곽선
WebkitTextStroke: "3px #000000"

// ❌ 너무 작은 텍스트
fontSize: 32  // 최소 48px 이상

// ❌ 얇은 폰트
fontWeight: 400  // 최소 700 이상
```

---

## 체크리스트

세그먼트 최종 확인 전:

- [ ] 콘텐츠가 `SafeContentArea` 또는 명시적 높이 제한 사용
- [ ] 콘텐츠에 `absolute bottom-XX` 포지셔닝 없음
- [ ] 콘텐츠가 850px 높이 안에 들어감
- [ ] 자막 타이밍에서 프리뷰 확인
- [ ] 자막 텍스트가 콘텐츠와 겹치지 않음
- [ ] 자막: 배경 없음, 흰색 font-black 56px
- [ ] 자막 위치: bottom-24
