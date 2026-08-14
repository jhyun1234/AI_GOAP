---
name: youtube-editor
description: YouTube 영상 편집 스킬. Remotion으로 자막 싱크, 오디오 트랜스크립션, 렌더링. 유튜브 영상, 팟캐스트 영상, 자막 추가, 캡션, 트랜스크립트, Whisper, 영상 렌더링, 16:9, 1080p 관련 키워드로 트리거됨.
---

# YouTube 편집기 스킬

Remotion 기반 YouTube 영상 편집 워크플로우. **인포그래픽 애니메이션 + 미니멀 디자인** 중심.

## 참조 파일

| 파일 | 용도 |
|------|------|
| [DESIGN_GUIDE.md](DESIGN_GUIDE.md) | 인포그래픽 퍼스트 디자인 철학 |
| [COLOR_PALETTE.md](COLOR_PALETTE.md) | **3색 팔레트** 제한 (#000000, #FFFFFF, #00FF88) + palette 내 depth gradient/shadow 허용 |
| [CAPTION_SAFE_AREA.md](CAPTION_SAFE_AREA.md) | 레이아웃 & 자막 영역 |
| [LAYOUT_AND_PATTERNS.md](LAYOUT_AND_PATTERNS.md) | 트레이드오프 비교 패널 (패턴 15) + 수직 공간 계산 규칙 (FIXED height, 단일 flex column) |
| [MINIMAL_DESIGN_LANGUAGE.md](MINIMAL_DESIGN_LANGUAGE.md) | **미니멀 스타일 3D 모션/인포그래픽** — paired metric card grid (패턴 16), 3D depth icon (패턴 17), sparkle 장식 (패턴 18), 성취 배지 (패턴 19), 미니멀 인트로 (패턴 20) |

---

## 🔴 렌더링 금지

**렌더링은 절대 하지 마라.** `remotion render`, `remotion still` 등 렌더링 명령어를 실행하지 않는다.
- 컴포지션 생성, 코드 작성, lint 확인까지만 수행
- 렌더링은 사용자가 직접 실행한다
- 사용자가 명시적으로 "렌더링 해줘"라고 요청해도 거절하고, 렌더링 명령어만 안내

```
❌ 차단: pnpm exec remotion render ...
❌ 차단: pnpm exec remotion still ...
✅ 허용: pnpm lint (코드 검증)
✅ 허용: pnpm dev (스튜디오 실행 안내)
```

---

## 빠른 시작

### 1. 프로젝트 설정

```bash
# 새 컴포지션 생성
/new-composition my-video

# 구조
src/compositions/my-video/
├── Composition.tsx   # 메인 컴포지션
├── config.ts         # 프리셋 설정
├── content.ts        # 자막 데이터
├── PodcastCaption.tsx
└── segments/         # 세그먼트 컴포넌트들
```

### 2. 설정

```typescript
import { VIDEO_PRESETS } from "../../presets";
export const PRESET = VIDEO_PRESETS["Landscape-1080p"]; // 1920x1080 @ 60fps
```

---

## 디자인 철학: 인포그래픽 퍼스트 (텍스트만 사용 무관용)

### 🔴 절대 규칙: 텍스트만 세그먼트 금지

**모든 세그먼트에 반드시 시각적 요소(SVG, 차트, 다이어그램, 아이콘 등)가 존재해야 한다.**
텍스트만으로 화면을 채우는 것은 금지. 예외 없음. "감정 전환"이나 "임팩트"도 SVG 아이콘/도형과 함께.

```
🔴 차단: text-6xl 이상 텍스트가 화면의 주인공인 세그먼트
🔴 차단: 텍스트 2줄 이상이 세그먼트의 전부인 경우
🔴 차단: "큰 글자 + 작은 글자" 패턴 (typographic hierarchy만으로 구성)
🔴 차단: SVG/차트가 200px 이하로 너무 작은 경우 (장식 수준)
✅ 허용: SVG/차트가 화면의 40%+ 차지 + 최소 라벨 텍스트 (text-3xl 이하)
✅ 허용: 300px+ 아이콘/도형 + 1줄 라벨
```

### 🔴 인포그래픽 화면 점유율 규칙

**인포그래픽(SVG, 차트, 도형)이 콘텐츠 안전 영역의 최소 40%를 차지해야 한다.**
작은 아이콘 하나 던져놓고 텍스트로 채우는 것은 인포그래픽이 아니다.

```
🔴 절대 규칙:
- 메인 인포그래픽 최소 크기: 300px (너비 또는 높이)
- 도넛/원형 차트: 최소 500px (권장 600px)
- 비교 바 전체 너비: 최소 1000px (권장 1200px)
- 노드맵/방사형: 최소 800x600px 영역
- 단일 SVG 아이콘이 메인인 경우: 최소 260px (권장 320px)
- 인포그래픽이 "장식"이 아닌 "주인공"이어야 한다

❌ 나쁨: 100px 아이콘 + text-7xl 텍스트 (아이콘이 장식)
❌ 나쁨: 200px 도넛 + 큰 텍스트 (도넛이 너무 작음)
✅ 좋음: 600px 도넛 + text-3xl 라벨 (도넛이 주인공)
✅ 좋음: 1200px 바 차트 + text-4xl 라벨 (차트가 주인공)
```

### 🔴 의미 없는 추상 도형 금지 — "로딩 스켈레톤" 패턴 차단

**모든 시각 요소는 의미를 가져야 한다.** 라벨 없는 가로 막대/네모를 단순 stack한 것은 UI 로딩 스켈레톤처럼 보일 뿐, 정보를 전달하지 못한다.

```
🔴 절대 금지:
- 라벨 없는 가로 막대들을 stack한 "코드 블록" 시각화
- 의미 없는 추상 도형 (회색/녹색 막대 반복)
- "코드처럼 보이는" 더미 바 차트 — 실제로는 정보 없음
- 단순 색상 alternation으로 시각 위계 만들기 (의미 부재)

✅ 올바른 접근:
- 각 블록에 의미 있는 라벨 (기능명, 파일명, 단계명)
- 시각 요소가 "무엇"을 나타내는지 라벨로 명시
- 색상 alternation은 시각 보조일 뿐, 정보는 라벨이 전달
```

**❌ 나쁜 예 — 로딩 스켈레톤처럼 보임:**

```tsx
// "거대한 코드"를 표현한답시고 라벨 없는 막대만 stack
{Array.from({ length: 18 }).map((_, i) => (
  <div style={{
    height: 16,
    width: `${widths[i]}%`,
    backgroundColor: i % 3 === 0 ? COLORS.accent : "rgba(255,255,255,0.5)",
  }} />
))}
// 결과: 시청자는 "이게 뭐지? 로딩 중인가?" 라고 생각함
```

**✅ 좋은 예 — 의미 있는 라벨 블록 타워:**

```tsx
// "AI가 한 번에 쏟아내는 기능들" — 각 블록에 기능명 명시
const FEATURES = [
  "배포 설정", "테스트", "결제 모듈", "인증",
  "라우팅", "프론트엔드 UI", "비즈니스 로직",
  "백엔드 API", "DB 스키마",
];

{FEATURES.map((feat, i) => (
  <div style={{
    backgroundColor: i % 2 === 0 ? COLORS.accent : "transparent",
    border: i % 2 === 0 ? "none" : "3px solid #FFFFFF",
    paddingLeft: 28,
  }}>
    <p style={{ color: i % 2 === 0 ? "#000" : "#FFF", fontSize: 28 }}>
      {feat}
    </p>
  </div>
))}
// 결과: 시청자는 즉시 "AI가 이 9가지 기능을 한 번에 만들었구나" 이해
```

**적용 원칙:**
- "코드/파일/기능이 많다"를 표현할 때 → **실제 기능명 라벨 블록**으로 시각화
- "데이터가 흐른다"를 표현할 때 → **실제 데이터 종류 라벨 카드**
- "단계가 많다"를 표현할 때 → **실제 단계명이 적힌 노드**
- 추상적 도형은 **반드시 라벨**과 함께 — 라벨 없는 도형은 장식일 뿐

### 인포그래픽 적용 점수

세그먼트를 만들기 전 점수 체크:
- SVG 도형/차트/아이콘이 화면 40%+ 차지하는가? → +4점
- SVG 도형/차트/아이콘이 있긴 한데 작은가? (40% 미만) → +2점
- 애니메이션이 데이터를 "그리는가"? (bar growth, line draw, counter) → +2점
- 인포그래픽이 300px 이상인가? → +1점
- 텍스트가 라벨 역할만 하는가? (text-3xl 이하, 보조 역할) → +1점
- 텍스트가 text-6xl 이상으로 화면 중앙에 있는가? → -5점
- 텍스트만으로 세그먼트가 구성되는가? → -10점 (자동 FAIL)

**최소 통과 점수: 4점. 2점 이하는 반드시 리팩터.**

### 핵심 원칙

1. **그림이 곧 설명이다** — 텍스트로 설명하지 마라, SVG로 그려서 보여줘라
2. **한 프레임, 한 인포그래픽** — 하나의 시각적 요소에 집중
3. **데이터를 시각화하라** — 숫자/비교/프로세스는 반드시 차트/다이어그램/바 등으로
4. **움직임이 이해를 돕는다** — 순차 빌드업으로 정보를 전달
5. **텍스트는 라벨이다** — 최대 text-3xl, 보조 역할만 (회색/반투명 금지, 흰색 또는 강조색)
6. **감정도 시각화하라** — "놀라움"은 느낌표 SVG, "질문"은 물음표 SVG, "성공"은 체크마크
7. **검정 배경에서 잘 보여야 한다** — 모든 인포그래픽은 #000000 위에 그려진다. 밝고 선명하게

### 🔴 인포그래픽 가시성 규칙 — 검정 배경 대비 필수

**배경은 항상 #000000이다.** 모든 SVG, 차트, 도형, 선, 아이콘은 검정 배경 위에서 **확실히 잘 보여야** 한다.

```
🔴 절대 규칙:
- SVG stroke/fill은 반드시 밝은 색 (#FFFFFF, #00FF88) 사용
- 선(line/path)은 strokeWidth 최소 3px — 얇으면 안 보임
- 도형 테두리(border)는 최소 3px — 1px은 검정에서 사라짐
- 배경 트랙(도넛 빈 부분, 프로그레스 빈 부분)은 rgba(255,255,255,0.1) 이상
- 연결선(AnimatedLine 등)도 최소 strokeWidth: 3
```

**SVG 색상 규칙:**

| 요소 | 색상 | 비고 |
|------|------|------|
| 메인 도형 fill/stroke | `#00FF88` | 유일한 강조색 — 눈에 확 띄어야 함 |
| 보조 도형 stroke | `#FFFFFF` | 흰색 테두리/선 |
| 빈 트랙 (도넛, 프로그레스 배경) | `rgba(255,255,255,0.1)` | 최소 0.1 — 0.05는 안 보임 |
| 연결선/가이드선 | `rgba(255,255,255,0.15)` 이상 | 최소 0.15 |
| 텍스트 (SVG 내) | `#FFFFFF` 또는 `#00FF88` | 회색 금지 |

```tsx
// ✅ 좋음: 밝고 선명한 인포그래픽
<svg>
  <circle stroke="#00FF88" strokeWidth={5} fill="none" />  // 굵고 밝음
  <rect fill="#00FF88" />  // 선명한 fill
  <line stroke="#FFFFFF" strokeWidth={3} />  // 흰색, 3px 이상
</svg>

// ❌ 나쁨: 검정 배경에서 안 보이는 인포그래픽
<svg>
  <circle stroke="rgba(255,255,255,0.3)" strokeWidth={1} />  // 너무 얇고 어둡다
  <rect fill="#333333" />  // 검정 배경에서 안 보임
  <line stroke="rgba(255,255,255,0.05)" />  // 거의 투명
</svg>

// ❌ 나쁨: 어두운 색 fill
<div style={{ backgroundColor: '#1a1a1a' }} />  // 검정과 구분 안 됨
<div style={{ backgroundColor: '#333' }} />  // 안 보임
```

### 🔴 한글 우선 규칙

**모든 텍스트는 반드시 한글로 작성한다.** 영어 라벨, 영어 카테고리명 금지. 고유명사(GPT, AI, Cursor 등)만 영어 허용.

```
❌ "COST COMPARISON" → ✅ "비용 비교"
❌ "SAVED TIME" → ✅ "절약한 시간"
❌ "EDIT CYCLE" → ✅ "편집 주기"
❌ "Deploy" → ✅ "배포"
✅ "GPT-3", "ChatGPT", "Cursor" (고유명사는 영어 OK)
```

### 🔴 가짜 수치 금지 규칙 — "스크립트에 없으면 만들어내지 마라"

**스크립트에 없는 수치/퍼센트/통계를 만들어내지 마라.** 임팩트를 위해 임의의 숫자(`₩1,000,000`, `10x`, `73%` 등)를 카운터/차트에 박는 것은 시청자에게 잘못된 정보를 주는 거짓말이다.

```
🔴 절대 금지:
- AnimatedCounter 값으로 임의 숫자(₩1,000,000, 1,000번 등) 박기 — 스크립트에 없으면 안 됨
- "효율 +73%" 같은 가짜 KPI 텍스트
- "1차 80% / 2차 50% / 3차 15%" 같은 발명한 데이터 (단, 추세를 표현하기 위한 상대값은 OK)
- "10배 더 빠르게" 같은 마케팅 수치

✅ 허용:
- 스크립트에 명시된 수치 ("800줄", "500줄 한도" 등)
- 추세/방향만 표현하는 상대 비교 (예: "예전 18%" vs "지금 100%" — 단위 없음, 추세만)
- 시각적 양 비교 (코인 스택 1개 vs 8개 — 정확한 숫자가 아닌 "더 많음" 표현)
```

**대안 — 가짜 수치 없이 "비싸졌다/늘어났다/줄어들었다"를 표현하는 방법:**

| 표현하고 싶은 것 | 가짜 수치 (금지) | 시각적 대안 (권장) |
|---------------|----------------|------------------|
| "비싸졌다" | `₩1,000,000` 카운터 | VerticalBar 비교 (예전 작음 vs 지금 큼, 단위 없음) |
| "많아졌다" | `+500%` 텍스트 | 코인/아이콘 개수 비교 (1개 vs 다수) |
| "빨라졌다" | `10x faster` | 프로그레스 바 길이 비교 |
| "심해졌다" | `3배 악화` | 색/크기 강도 변화 |
| "줄었다" | `-80%` | FunnelChart 단계별 축소 (단위 없이) |

```tsx
// ❌ 나쁨: 가짜 카운터
<AnimatedCounter value={1000000} prefix="₩" />  // 스크립트에 ₩가 없는데 박았다

// ✅ 좋음: 단위 없는 추세 비교
<VerticalBar label="예전" heightPercent={18} />
<VerticalBar label="지금" heightPercent={100} isAccent />
// 추세만 보여주고 정확한 숫자는 안 보여줌

// ✅ 좋음: 양 비교 (수치 없음)
<CoinStack count={1} />  vs  <CoinStack count={8} />
```

**💡 적용 원칙**: 스크립트에 "비싸졌습니다"만 있으면, 시각도 "비싸졌다"만 표현하면 된다. 정확한 숫자를 발명할 필요 없음.

### 🔴 3색 규칙 — 검정 + 흰색 + 네온 그린만

**3색만 사용한다. 예외 없음.** 골드(#FFD700), 레드(#FF3232)는 삭제됨.

| 용도 | 색상 | 코드 |
|------|------|------|
| 기본 텍스트/라벨 | **흰색** | `#FFFFFF` |
| 보조 라벨 | **반투명 흰색 (최소 0.7)** | `rgba(255,255,255,0.7)` |
| **모든 강조** (긍정/핵심/데이터/비교) | **네온 그린** | `#00FF88` |

비교/대비는 **색이 아닌 위계(크기/굵기)와 맥락(아이콘/텍스트)** 으로 표현한다:
- 좋은 것 = `#00FF88` (강조)
- 나쁜 것/대조 대상 = `#FFFFFF` (중립)

```
❌ style={{ color: '#FFD700' }}  // 골드 삭제됨
❌ style={{ color: '#FF3232' }}  // 레드 삭제됨
❌ style={{ color: 'rgba(255,255,255,0.4)' }}  // 안 보임
❌ className="text-gray-600"  // Tailwind 회색 금지
✅ style={{ color: '#FFFFFF' }}  // 기본
✅ style={{ color: 'rgba(255,255,255,0.7)' }}  // 보조 라벨 (최소치)
✅ style={{ color: '#00FF88' }}  // 유일한 강조색
```

### 🔴 글자 크기 규칙 — 크게 넣어라

1920x1080에서 글자가 작으면 안 보인다. **항상 한 단계 크게** 넣어라.

| 용도 | 최소 크기 | 권장 크기 | 비고 |
|------|----------|----------|------|
| 히어로 카운터 | text-8xl (96px) | **text-9xl (128px)** | 도넛/카운터 숫자 |
| 데이터 값 | text-7xl (72px) | **text-8xl (96px)** | 차트 내 숫자 |
| 항목 라벨 | text-4xl (36px) | **text-5xl (48px)** | 바 차트, 프로세스 라벨 |
| 비교 헤더 | text-5xl (48px) | **text-6xl (60px)** | 좌우 분할 제목 |
| 카테고리 라벨 | text-3xl (30px) | **text-4xl (36px)** | 섹션 라벨 (rgba 0.7 또는 #FFFFFF) |
| 보조 설명 | text-2xl (24px) | **text-3xl (30px)** | 서브 텍스트 |

### 텍스트 허용 범위:

```tsx
// ✅ 라벨 (인포그래픽 위/아래 — 한글, 크게)
<p className="text-4xl tracking-widest"
   style={{ color: 'rgba(255,255,255,0.7)' }}>비용 비교</p>

// ✅ 데이터 값 (차트 안의 숫자)
<p className="text-9xl font-black tabular-nums" style={{ color: '#00FF88' }}>₩0</p>

// ❌ 절대 금지: 텍스트가 세그먼트의 주인공
<p className="text-9xl font-black">미친거 하나</p>  // ← 차단
<p className="text-8xl font-black">시작해볼까요?</p>  // ← 차단

// ❌ 절대 금지: 영어 라벨
<p>COST COMPARISON</p>  // ← 차단, "비용 비교"로
<p>SAVED TIME</p>       // ← 차단, "절약한 시간"으로
```

---

## 인포그래픽 애니메이션 패턴 (주요)

모든 세그먼트는 **인포그래픽 우선**으로 설계한다. 아래 패턴 중 가장 적합한 것을 선택.

### 패턴 1: 애니메이션 카운터 (숫자 카운팅)

```tsx
const AnimatedCounter: React.FC<{
  value: number;
  startFrame: number;
  duration?: number;
  prefix?: string;
  suffix?: string;
  color?: string;
}> = ({ value, startFrame, duration = 40, prefix = '', suffix = '', color = '#FFFFFF' }) => {
  const frame = useCurrentFrame();

  const progress = interpolate(
    frame,
    [startFrame, startFrame + duration],
    [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const eased = 1 - Math.pow(1 - progress, 3);
  const current = Math.round(eased * value);

  return (
    <p className="text-9xl font-black tabular-nums" style={{ color }}>
      {prefix}{current.toLocaleString()}{suffix}
    </p>
  );
};
```

### 패턴 2: 비교 막대 (비교 바)

```tsx
const ComparisonBar: React.FC<{
  label: string;
  value: number;
  maxValue: number;
  color: string;
  startFrame: number;
  index: number;
}> = ({ label, value, maxValue, color, startFrame, index }) => {
  const frame = useCurrentFrame();
  const delay = startFrame + index * 20;

  const barWidth = interpolate(
    frame,
    [delay, delay + 30],
    [0, (value / maxValue) * 100],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const labelOpacity = interpolate(
    frame,
    [delay - 10, delay],
    [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div className="w-full flex items-center gap-10" style={{ opacity: labelOpacity }}>
      <p className="text-4xl font-bold w-64 text-right" style={{ color: '#FFFFFF' }}>
        {label}
      </p>
      <div className="flex-1 h-20 rounded-full overflow-hidden"
           style={{ backgroundColor: 'rgba(255,255,255,0.1)' }}>
        <div
          className="h-full rounded-full"
          style={{ width: `${barWidth}%`, backgroundColor: color }}
        />
      </div>
    </div>
  );
};
```

### 패턴 2-B: 수직 비교 막대 (Vertical Bar) — 후킹/임팩트용

좌우 두 막대로 추세/대조를 보여주는 패턴. 후킹 세그먼트의 "예전 vs 지금" 같은 직접 비교에 적합.

```tsx
const VerticalBar: React.FC<{
  label: string;
  heightPercent: number;     // 0~100, 추세 표현 (정확한 수치 X — 가짜 수치 금지)
  maxHeight: number;          // 🔴 1080p에서 최대 400px (안전 영역 계산 참조)
  color: string;
  startFrame: number;
  isAccent?: boolean;
  index: number;
}> = ({ label, heightPercent, maxHeight, color, startFrame, isAccent, index }) => {
  const frame = useCurrentFrame();
  const delay = startFrame + index * 30;
  const targetHeight = (heightPercent / 100) * maxHeight;

  const animatedHeight = interpolate(
    frame, [delay, delay + 45], [0, targetHeight],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div className="flex flex-col items-center"
         style={{ height: maxHeight + 80, justifyContent: "flex-end" }}>
      <div className="rounded-t-2xl"
           style={{
             width: 200,
             height: animatedHeight,
             backgroundColor: color,
             boxShadow: isAccent ? `0 0 50px #00FF8866` : undefined,
           }} />
      <p className="font-black mt-5"
         style={{ color: isAccent ? '#00FF88' : '#FFFFFF', fontSize: 44 }}>
        {label}
      </p>
    </div>
  );
};

// 사용: 추세 비교 (단위 없음 — 추세만)
<VerticalBar label="예전" heightPercent={18} maxHeight={400}
             color="rgba(255,255,255,0.85)" startFrame={400} index={0} />
<VerticalBar label="지금" heightPercent={100} maxHeight={400}
             color="#00FF88" startFrame={400} index={1} isAccent />
```

### 🔴 콘텐츠 안전 영역 높이 계산 (1080p 기준)

**1080p 콘텐츠 안전 영역 = 1080 - 230 (자막 paddingBottom) = 850px**

이 850px 안에 모든 요소(상단 라벨 + 메인 인포그래픽 + 임팩트 텍스트)가 들어가야 한다.

| 컴포넌트 | 권장 최대 크기 |
|---------|-------------|
| 단일 SVG 아이콘 (메인) | 320~360px |
| 도넛 차트 | 500~600px (정사각형) |
| **수직 막대 차트 (maxHeight)** | **400px** |
| 가로 비교 막대 (전체 너비) | 1100~1400px |
| 노드맵 (영역) | 1100x640px (가로 우선) |

**수직 막대 차트 레이아웃 계산 (실패 사례 → 수정):**

```
❌ 처음 디자인 (제목 잘림 + 임팩트 텍스트가 자막과 겹침):
- 상단 라벨 (font 36 + mb-10):        80px
- 차트 (maxHeight 540 + 라벨 100):   640px
- 임팩트 텍스트 (font 88 + mt-12):   140px
- 합계:                              860px  ← 850px 초과!

✅ 수정 디자인 (안전):
- 상단 라벨 (font 32 + mb-8):         60px
- 차트 (maxHeight 400 + 라벨 80):    480px
- 임팩트 텍스트 (font 76 + mt-10):   120px
- 합계:                              660px  ← 850px 안에 fit
```

**규칙:**
1. 메인 인포그래픽 + 상단 라벨 + 하단 임팩트 텍스트 합계가 **850px를 넘으면 안 됨**
2. 의심스러우면 maxHeight를 **400px 이하**로 제한
3. 임팩트 텍스트는 **fontSize 80 이하** 권장
4. 상단 라벨은 **fontSize 36 이하 + mb-8 이하** 권장
5. 빌드 후 still 이미지로 자막 영역(하단 230px)과 임팩트 텍스트가 겹치지 않는지 확인

### 패턴 3: 원형/도넛 차트

```tsx
const DonutSegment: React.FC<{
  percentage: number;
  color: string;
  startFrame: number;
  size?: number;
  strokeWidth?: number;
}> = ({ percentage, color, startFrame, size = 560, strokeWidth = 36 }) => {
  const frame = useCurrentFrame();
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;

  const progress = interpolate(
    frame,
    [startFrame, startFrame + 40],
    [0, percentage / 100],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const dashOffset = circumference * (1 - progress);

  return (
    <svg width={size} height={size} style={{ transform: 'rotate(-90deg)' }}>
      <circle
        cx={size / 2} cy={size / 2} r={radius}
        fill="none" stroke="rgba(255,255,255,0.1)" strokeWidth={strokeWidth}
      />
      <circle
        cx={size / 2} cy={size / 2} r={radius}
        fill="none" stroke={color} strokeWidth={strokeWidth}
        strokeDasharray={circumference} strokeDashoffset={dashOffset}
        strokeLinecap="round"
      />
    </svg>
  );
};

// 사용: 도넛 + 중앙 텍스트
<SafeContentArea>
  <div className="relative flex items-center justify-center">
    <DonutSegment percentage={73} color="#00FF88" startFrame={20} size={600} />
    <div className="absolute text-center">
      <AnimatedCounter value={73} startFrame={30} suffix="%" color="#00FF88" />
    </div>
  </div>
</SafeContentArea>
```

### 패턴 4: 프로세스 흐름

```tsx
const ProcessStep: React.FC<{
  number: number;
  label: string;
  startFrame: number;
  isActive: boolean;
}> = ({ number, label, startFrame, isActive }) => {
  const frame = useCurrentFrame();

  const opacity = interpolate(
    frame, [startFrame, startFrame + 15], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const slideX = interpolate(
    frame, [startFrame, startFrame + 20], [-30, 0],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div className="flex items-center gap-8"
         style={{ opacity, transform: `translateX(${slideX}px)` }}>
      <div
        className="w-26 h-26 rounded-full flex items-center justify-center text-4xl font-black"
        style={{
          width: 104, height: 104,
          backgroundColor: isActive ? '#00FF88' : 'rgba(255,255,255,0.15)',
          color: isActive ? '#000000' : 'rgba(255,255,255,0.7)',
        }}
      >
        {number}
      </div>
      <p className="text-5xl font-bold"
         style={{ color: isActive ? '#FFFFFF' : 'rgba(255,255,255,0.7)' }}>
        {label}
      </p>
    </div>
  );
};
```

### 패턴 5: SVG 선 그리기 (선/화살표 애니메이션)

```tsx
const AnimatedLine: React.FC<{
  startFrame: number;
  duration?: number;
  from: { x: number; y: number };
  to: { x: number; y: number };
  color?: string;
  strokeWidth?: number;
}> = ({ startFrame, duration = 30, from, to, color = '#00FF88', strokeWidth = 3 }) => {
  const frame = useCurrentFrame();

  const length = Math.sqrt(
    Math.pow(to.x - from.x, 2) + Math.pow(to.y - from.y, 2)
  );

  const progress = interpolate(
    frame, [startFrame, startFrame + duration], [length, 0],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <svg width="100%" height="100%" style={{ position: 'absolute', top: 0, left: 0 }}>
      <line
        x1={from.x} y1={from.y} x2={to.x} y2={to.y}
        stroke={color} strokeWidth={strokeWidth}
        strokeDasharray={length} strokeDashoffset={progress}
        strokeLinecap="round"
      />
    </svg>
  );
};
```

### 패턴 6: 아이콘/로고 등장 애니메이션

```tsx
const IconReveal: React.FC<{
  children: React.ReactNode;
  startFrame: number;
  size?: number;
}> = ({ children, startFrame, size = 200 }) => {
  const frame = useCurrentFrame();

  const scale = interpolate(
    frame, [startFrame, startFrame + 15, startFrame + 25], [0.5, 1.1, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [startFrame, startFrame + 10], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div style={{
      width: size, height: size, opacity,
      transform: `scale(${scale})`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }}>
      {children}
    </div>
  );
};
```

### 패턴 7: 타임라인 애니메이션

```tsx
const TimelineItem: React.FC<{
  year: string;
  label: string;
  startFrame: number;
  index: number;
  isHighlight?: boolean;
}> = ({ year, label, startFrame, index, isHighlight = false }) => {
  const frame = useCurrentFrame();
  const delay = startFrame + index * 25;

  const opacity = interpolate(
    frame, [delay, delay + 15], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const dotScale = interpolate(
    frame, [delay, delay + 12, delay + 20], [0, 1.3, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 이전 점에서 이 점까지 선이 자란다
  const lineProgress = interpolate(
    frame, [delay - 15, delay], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const dotColor = isHighlight ? '#00FF88' : 'rgba(255,255,255,0.7)';
  const textColor = isHighlight ? '#FFFFFF' : 'rgba(255,255,255,0.7)';

  return (
    <div className="flex flex-col items-center gap-4" style={{ opacity }}>
      {/* 연결선 (첫 번째 항목 제외) */}
      {index > 0 && (
        <div className="w-1 h-20 -mb-4" style={{
          backgroundColor: 'rgba(255,255,255,0.15)',
          transform: `scaleY(${lineProgress})`,
          transformOrigin: 'top',
        }} />
      )}
      {/* 점 */}
      <div
        className="w-8 h-8 rounded-full"
        style={{ backgroundColor: dotColor, transform: `scale(${dotScale})` }}
      />
      {/* 연도 + 라벨 */}
      <p className="text-3xl font-bold tracking-wider" style={{ color: dotColor }}>
        {year}
      </p>
      <p className="text-5xl font-bold" style={{ color: textColor }}>
        {label}
      </p>
    </div>
  );
};

// 사용: 가로 타임라인
<SafeContentArea>
  <div className="flex items-start gap-20">
    <TimelineItem year="2020" label="GPT-3" startFrame={20} index={0} />
    <TimelineItem year="2022" label="ChatGPT" startFrame={20} index={1} />
    <TimelineItem year="2024" label="Cursor" startFrame={20} index={2} isHighlight />
    <TimelineItem year="2025" label="바이브 코딩" startFrame={20} index={3} isHighlight />
  </div>
</SafeContentArea>
```

### 패턴 8: 노드 맵 (노드 연결 다이어그램)

```tsx
const NodeItem: React.FC<{
  label: string;
  x: number;
  y: number;
  startFrame: number;
  color?: string;
  size?: number;
}> = ({ label, x, y, startFrame, color = '#00FF88', size = 220 }) => {
  const frame = useCurrentFrame();

  const scale = interpolate(
    frame, [startFrame, startFrame + 12, startFrame + 20], [0, 1.15, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [startFrame, startFrame + 10], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div style={{
      position: 'absolute', left: x - size / 2, top: y - size / 2,
      width: size, height: size, opacity, transform: `scale(${scale})`,
      display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center',
    }}>
      <div className="w-full h-full rounded-full flex items-center justify-center"
           style={{ backgroundColor: `${color}22`, border: `4px solid ${color}` }}>
        <p className="text-3xl font-black text-center leading-tight px-4"
           style={{ color }}>
          {label}
        </p>
      </div>
    </div>
  );
};

// AnimatedLine으로 노드 간 연결선 + NodeItem으로 노드 배치
// 사용 예: 기술 관계도, 워크플로우, 의존성 맵
```

### 패턴 9: 좌우 분할 비교

```tsx
const SplitComparison: React.FC<{
  leftLabel: string;
  rightLabel: string;
  leftItems: string[];
  rightItems: string[];
  startFrame: number;
  leftColor?: string;
  rightColor?: string;
}> = ({
  leftLabel, rightLabel, leftItems, rightItems, startFrame,
  leftColor = '#FFFFFF', rightColor = '#00FF88'
}) => {
  const frame = useCurrentFrame();

  // 구분선이 아래로 그려진다
  const dividerHeight = interpolate(
    frame, [startFrame, startFrame + 25], [0, 100],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div className="w-full max-w-6xl flex relative" style={{ minHeight: 520 }}>
      {/* 왼쪽 */}
      <div className="flex-1 flex flex-col items-center gap-8 pr-16">
        <p className="text-5xl font-black" style={{ color: leftColor }}>{leftLabel}</p>
        {leftItems.map((item, i) => {
          const delay = startFrame + 30 + i * 15;
          const itemOpacity = interpolate(
            frame, [delay, delay + 12], [0, 1],
            { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
          );
          return (
            <p key={i} className="text-4xl font-bold"
               style={{ color: 'rgba(255,255,255,0.7)', opacity: itemOpacity }}>
              {item}
            </p>
          );
        })}
      </div>

      {/* 중앙 구분선 */}
      <div className="absolute left-1/2 top-0 bottom-0 w-0.5"
           style={{
             backgroundColor: 'rgba(255,255,255,0.2)',
             transform: `scaleY(${dividerHeight / 100})`,
             transformOrigin: 'top',
           }} />

      {/* 오른쪽 */}
      <div className="flex-1 flex flex-col items-center gap-8 pl-16">
        <p className="text-5xl font-black" style={{ color: rightColor }}>{rightLabel}</p>
        {rightItems.map((item, i) => {
          const delay = startFrame + 35 + i * 15;
          const itemOpacity = interpolate(
            frame, [delay, delay + 12], [0, 1],
            { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
          );
          return (
            <p key={i} className="text-4xl font-bold"
               style={{ color: 'rgba(255,255,255,0.7)', opacity: itemOpacity }}>
              {item}
            </p>
          );
        })}
      </div>
    </div>
  );
};
```

### 패턴 10: 지표 카드 쌓기

```tsx
const MetricCard: React.FC<{
  label: string;
  value: string;
  startFrame: number;
  index: number;
  accentColor?: string;
}> = ({ label, value, startFrame, index, accentColor = '#00FF88' }) => {
  const frame = useCurrentFrame();
  const delay = startFrame + index * 18;

  const opacity = interpolate(
    frame, [delay, delay + 15], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const slideY = interpolate(
    frame, [delay, delay + 18], [40, 0],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div style={{ opacity, transform: `translateY(${slideY}px)` }}
         className="flex items-baseline gap-8">
      <div className="w-4 h-20 rounded-full" style={{ backgroundColor: accentColor }} />
      <div>
        <p className="text-3xl font-medium tracking-wider uppercase"
           style={{ color: 'rgba(255,255,255,0.7)' }}>
          {label}
        </p>
        <p className="text-8xl font-black -mt-2" style={{ color: '#FFFFFF' }}>
          {value}
        </p>
      </div>
    </div>
  );
};

// 사용: 핵심 수치를 순차적으로 쌓아 올리기
<SafeContentArea>
  <div className="flex flex-col gap-8">
    <MetricCard label="개발 비용" value="₩0" startFrame={20} index={0} accentColor="#00FF88" />
    <MetricCard label="개발 시간" value="3일" startFrame={20} index={1} accentColor="#00FF88" />
    <MetricCard label="코드 라인" value="2,400" startFrame={20} index={2} accentColor="#00FF88" />
  </div>
</SafeContentArea>
```

### 패턴 11: 애니메이션 다이어그램 (Mermaid/D2 활용)

기존 `Diagram.tsx` 컴포넌트를 활용하여 복잡한 관계도, 플로우차트, 시퀀스 다이어그램을 렌더링.

```tsx
import { Diagram } from "../../../components/Diagram";

// Mermaid 플로우차트
<SafeContentArea>
  <div className="w-full max-w-6xl" style={{ height: 650 }}>
    <Diagram
      type="mermaid"
      diagram={`graph LR
        A[아이디어] --> B[프롬프트]
        B --> C[AI 생성]
        C --> D[배포]
        style A fill:#FFFFFF,stroke:#FFFFFF,color:#000
        style D fill:#00FF88,stroke:#00FF88,color:#000
      `}
      theme="dark"
      backgroundColor="transparent"
    />
  </div>
</SafeContentArea>

// D2 다이어그램 (스케치 스타일)
<Diagram
  type="d2"
  diagram={`
    user -> cursor: 프롬프트
    cursor -> app: 생성
    app -> deploy: 배포
  `}
  sketch={true}
  backgroundColor="transparent"
/>
```

### 패턴 12: 방사형 배치

```tsx
const RadialItem: React.FC<{
  label: string;
  angle: number;    // 각도 (0-360)
  radius: number;   // 중심에서 거리
  startFrame: number;
  index: number;
  color?: string;
}> = ({ label, angle, radius, startFrame, index, color = '#00FF88' }) => {
  const frame = useCurrentFrame();
  const delay = startFrame + index * 12;

  const radians = (angle * Math.PI) / 180;
  const x = Math.cos(radians) * radius;
  const y = Math.sin(radians) * radius;

  const scale = interpolate(
    frame, [delay, delay + 10, delay + 18], [0, 1.15, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [delay, delay + 8], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 중심에서 항목까지 선
  const lineProgress = interpolate(
    frame, [delay - 5, delay + 5], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <>
      {/* 연결선 */}
      <line
        x1="50%" y1="50%"
        x2={`${50 + (x / radius) * 30}%`}
        y2={`${50 + (y / radius) * 30}%`}
        stroke="rgba(255,255,255,0.15)"
        strokeWidth={2}
        strokeDasharray={radius}
        strokeDashoffset={radius * (1 - lineProgress)}
      />
      {/* 노드 */}
      <div style={{
        position: 'absolute',
        left: `calc(50% + ${x}px)`,
        top: `calc(50% + ${y}px)`,
        transform: `translate(-50%, -50%) scale(${scale})`,
        opacity,
      }}>
        <div className="px-8 py-4 rounded-full"
             style={{ backgroundColor: `${color}22`, border: `3px solid ${color}` }}>
          <p className="text-3xl font-bold whitespace-nowrap" style={{ color }}>
            {label}
          </p>
        </div>
      </div>
    </>
  );
};

// 사용: 중심 키워드 + 방사형 관련 요소
<SafeContentArea>
  <div className="relative" style={{ width: 1040, height: 780 }}>
    <svg className="absolute inset-0 w-full h-full">
      {/* RadialItem에서 선 렌더링 */}
    </svg>
    {/* 중심 노드 */}
    <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2">
      <div className="w-42 h-42 rounded-full flex items-center justify-center"
           style={{ width: 168, height: 168, backgroundColor: '#00FF88' }}>
        <p className="text-4xl font-black" style={{ color: '#000000' }}>AI</p>
      </div>
    </div>
    {/* 주변 항목 */}
    <RadialItem label="코드" angle={0} radius={260} startFrame={30} index={0} />
    <RadialItem label="디자인" angle={72} radius={260} startFrame={30} index={1} />
    <RadialItem label="배포" angle={144} radius={260} startFrame={30} index={2} />
    <RadialItem label="테스트" angle={216} radius={260} startFrame={30} index={3} />
    <RadialItem label="문서" angle={288} radius={260} startFrame={30} index={4} />
  </div>
</SafeContentArea>
```

### 패턴 13: 펀넬 차트 (단계별 감소)

```tsx
const FunnelChart: React.FC<{
  steps: { label: string; value: number; color: string }[];
  startFrame: number;
  maxWidth?: number;
}> = ({ steps, startFrame, maxWidth = 1000 }) => {
  const frame = useCurrentFrame();
  const maxValue = Math.max(...steps.map(s => s.value));

  return (
    <div className="flex flex-col items-center gap-4" style={{ width: maxWidth }}>
      {steps.map((step, i) => {
        const delay = startFrame + i * 20;
        const barWidthPct = (step.value / maxValue) * 100;

        const width = interpolate(
          frame, [delay, delay + 30], [0, barWidthPct],
          { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
        );

        const opacity = interpolate(
          frame, [delay - 5, delay + 5], [0, 1],
          { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
        );

        return (
          <div key={i} className="flex items-center gap-8 w-full" style={{ opacity }}>
            <p className="text-4xl font-bold w-48 text-right" style={{ color: '#FFFFFF' }}>
              {step.label}
            </p>
            <div className="flex-1 flex justify-center">
              <div
                className="h-16 rounded-lg flex items-center justify-center"
                style={{
                  width: `${width}%`,
                  backgroundColor: step.color,
                  minWidth: width > 0 ? 80 : 0,
                }}
              >
                <p className="text-3xl font-black" style={{ color: '#000000' }}>
                  {step.value.toLocaleString()}
                </p>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

// 사용: 전환율, 단계별 이탈, 프로세스 효율
<SafeContentArea>
  <FunnelChart
    steps={[
      { label: "방문", value: 10000, color: "#FFFFFF" },
      { label: "클릭", value: 3500, color: "#FFFFFF" },
      { label: "가입", value: 800, color: "#00FF88" },
      { label: "구매", value: 120, color: "#00FF88" },
    ]}
    startFrame={20}
    maxWidth={1100}
  />
</SafeContentArea>
```

### 🔴 패턴 14 (삭제됨): 펄스 링 / 동심원 ripple 애니메이션 — **금지**

**펄스 링(동심원 확장 + 페이드 loop) 애니메이션은 사용 금지.** 메인 비주얼 주변에 계속 ripple이 퍼져나가는 효과는 **시청자에게 어지러움을 유발**한다. 사각 카드든 원형 아이콘이든 동일하게 적용 — concentric rounded-square 또는 concentric circle ripple 모두 금지.

```
❌ 절대 금지:
- absolute 위치에 동심원/동심사각으로 scale 확장 + opacity 페이드를 loop하는 ring
- 메인 카드/아이콘 주변에 무한 반복되는 ripple 효과
- "숨쉬는 듯한" 펄스 효과 (반복 사이클)

✅ 정적 구간을 채우는 대안:
- **순차 빌드업**: 카드 안 내용물을 시간차로 fade-in (예: 4줄짜리 토큰을 30프레임마다 한 줄씩)
- **카운터/프로그레스 변화**: AnimatedCounter, 차트 grow 애니메이션
- **단일 진입 애니메이션**: scale 0.7→1.1→1.0 한 번 (loop 없음)
- **장면 전환**: 3초+ 정적 구간이 나오면 그 자체가 신호 — 새 phase로 전환하거나 새 인포그래픽 추가
```

> 💡 **이유**: 검정 배경 + 네온 그린 stroke ring이 화면 전체에서 무한 반복으로 확장되면 시청자의 시선이 안정되지 않고 어지러움을 느낀다. 특히 사각 카드 주변의 동심 rounded-square는 layered shadow처럼 보여 더 거슬린다. 시청자가 자막을 읽는 동안 배경에서 무언가가 계속 움직이면 인지 부하가 증가한다.

---

### 🔴 차트/막대 사이 dashed connector 화살표 금지

**막대 차트의 두 막대 사이에 dashed line + 화살표 머리로 "감소/증가"를 표현하지 마라.** 막대 top 좌표를 SVG 좌표로 정확히 맞추기가 어려워 100% 어색한 결과가 나온다 — 화살표가 막대 위 허공을 지나거나 엉뚱한 위치에 떠 있는 모습이 됨.

```
❌ 절대 금지:
- "예전" 막대 top → "지금" 막대 top 으로 그어지는 dashed line + 화살표
- 차트 두 데이터 포인트 사이를 잇는 비스듬한 점선
- "÷2" / "↓" 같은 메타 정보를 막대 사이 빈 공간에 그래픽으로 그리기

✅ 대안:
- 막대 차트 + **하단 callout 텍스트**로 변화량 표현 ("절반으로 ↓", "3배 증가" 등)
- 작아진 막대에 **glow/boxShadow** 강도를 점점 증가시켜 강조
- 두 막대 차이를 **수직 텍스트**로 라벨링 (각 막대 위에 "100%", "50%" 등 — 단, 가짜 수치 금지 규칙 주의)
- "지금" 막대가 자라는 동안 동시에 callout이 슬라이드 인
```

```tsx
// ❌ 나쁨: 비스듬한 점선이 어색하게 막대 위 허공을 지남
<svg style={{ position: "absolute" }}>
  <line x1={0} y1={4} x2={240} y2={180}
        stroke="#00FF88" strokeWidth={5} strokeDasharray="10,8" />
  <polygon points="..." fill="#00FF88" />  {/* 화살표 머리 */}
</svg>

// ✅ 좋음: callout 텍스트로 깔끔하게
<div className="flex">
  <Bar height={maxH} />
  <Bar height={maxH * 0.5} accent />
</div>
<p style={{ color: "#00FF88", fontSize: 64 }}>절반으로 ↓</p>
```

---

### 🔴 빈 시간 채우기 패턴 — 스켈레톤 + 진행형 칩

**자막은 흐르는데 비주얼이 정적인 3초+ 구간**(예: 메인 인포그래픽이 등장한 뒤 다음 인포그래픽까지의 갭)에 어지러운 펄스 링 대신 사용할 수 있는 안전한 패턴들.

#### 패턴 A: 스켈레톤 + 콘텐츠 reveal

대상 인포그래픽의 **빈 외곽선**(border outline)만 먼저 등장시키고, 실제 데이터/색상은 나중에 자막 sync에 맞춰 reveal.

```tsx
// 페이지 카드 3개 — 일찍 outline만 등장, color word 나올 때 색 reveal
const pages = [
  { label: "페이지 A", color: "#A855F7", skeletonTrigger: 50,  revealTrigger: 322 },
  { label: "페이지 B", color: "#4F46E5", skeletonTrigger: 110, revealTrigger: 406 },
  { label: "페이지 C", color: "#9CA3AF", skeletonTrigger: 170, revealTrigger: 500 },
];

{pages.map((p) => {
  const skeletonOp = interpolate(frame, [p.skeletonTrigger, p.skeletonTrigger + 18], [0, 1], ...);
  const revealOp   = interpolate(frame, [p.revealTrigger, p.revealTrigger + 18], [0, 1], ...);
  const glowOp     = interpolate(frame,
    [p.revealTrigger - 4, p.revealTrigger + 12, p.revealTrigger + 36], [0, 1, 0], ...);
  return (
    <div style={{
      border: `3px solid ${revealOp > 0.3 ? "#00FF88" : "rgba(255,255,255,0.25)"}`,
      opacity: skeletonOp,
      boxShadow: `0 0 ${40 * glowOp}px rgba(0,255,136,${0.4 * glowOp})`,
    }}>
      <p>{p.label}</p>  {/* 항상 보임 (skeleton 단계) */}
      <ColorSwatch style={{ opacity: revealOp }} />  {/* sync 시 reveal */}
    </div>
  );
})}
```

#### 패턴 B: 진행형 칩 (Benefit Chips)

긴 세그먼트(8s+)에 4개의 작은 칩을 자막 흐름에 맞춰 1.5s 간격으로 등장. 각 칩은 ✓ 체크마크 + 짧은 라벨.

```tsx
const chips = [
  { text: "비개발자도 OK", trigger: 220 },  // caption 16.66+
  { text: "한 번만 작성",  trigger: 290 },  // caption 18.98+
  { text: "재사용 무한",   trigger: 380 },  // caption 20.7+
  { text: "시간 낭비 없음", trigger: 490 },  // caption 22.66+
];
{chips.map(c => <Chip {...c} />)}  // pill border, ✓ icon, slide+fade in
```

**규칙**:
1. 각 trigger는 해당 시점 자막 시작 프레임 ±20 안으로 정렬
2. 한 칩과 다음 칩 사이 **1~1.7초** (60-100 프레임) 간격 → 시청자가 읽을 시간 확보 + 정적 구간 없음
3. 칩 텍스트는 **8자 이내**로 짧게 (`text-3xl ~ 4xl`)
4. 4개 이상이면 두 줄로 wrap. 6개 넘기면 메트릭 카드(패턴 10)로 전환

---

## 🔴 인포그래픽 ↔ 텍스트 간격 규칙 — 숨 쉴 공간 필수

**인포그래픽(SVG/차트/도형/아이콘)과 그 위/아래 라벨·캡션·임팩트 텍스트 사이는 반드시 충분히 떨어져야 한다.** 1080p 화면에서 36-40px 간격은 너무 좁아 "텍스트가 인포에 붙어있다"는 느낌을 준다.

```
🔴 절대 규칙 (1080p 기준):
- 인포그래픽 ↔ 라벨/캡션 간격: 최소 60px, 권장 70-80px
- 메인 인포그래픽 ↔ 하단 임팩트 텍스트: 최소 72px (권장 80-90px)
- 상단 카테고리 라벨 ↔ 메인 인포그래픽: 최소 48px (권장 60-70px)
- Phase 내 텍스트 그룹과 다른 그룹 사이: 최소 60px
- 한 텍스트 블록 안에 두 줄 (제목 + 부제목): 12-20px만 사용 (의도적 짝)
```

```tsx
// ❌ 나쁨 — 인포와 텍스트 붙음 (시각적으로 답답)
<InfographicComponent />
<p style={{ marginTop: 28 }}>임팩트 텍스트</p>

// ❌ 나쁨 — 카드와 하단 callout 간격 32-40px (가까움)
<CardGrid />
<p style={{ marginTop: 32 }}>callout</p>

// ✅ 좋음 — 빈 spacer div로 명확한 간격
<InfographicComponent />
<div style={{ height: 72 }} />
<p style={{ margin: 0 }}>임팩트 텍스트</p>

// ✅ 좋음 — marginTop 60+ 직접 지정
<CardGrid />
<p style={{ marginTop: 72 }}>callout</p>

// ✅ 좋음 — 의도적으로 묶인 두 줄 (제목+부제목은 가깝게 OK)
<p style={{ fontSize: 56 }}>주제목</p>
<p style={{ fontSize: 30, marginTop: 12 }}>부제목</p>
```

**왜 spacer div를 권장하는가?**
1. 시각적으로 의도가 명확 — `height: 72`만 보면 즉시 간격 확인 가능
2. opacity 애니메이션과 충돌 없음 — margin이 transition을 일으키지 않음
3. flex layout에서 `gap`보다 세밀한 제어 가능

### 점검 방법

세그먼트별로 still 캡처를 보면서:
1. 인포그래픽과 텍스트 사이에 손가락 1개 폭 (~60px) 이상 비어있는가?
2. 텍스트가 인포그래픽에 "달라붙어" 보이지 않는가?
3. 라벨이 인포의 일부처럼 보이지 않고 독립된 요소로 인식되는가?

→ 답이 No면 spacer div로 간격 60-80px 추가.

---

## 🔴 레이아웃 안정성 규칙 — 레이아웃 시프트 금지

**애니메이션으로 인해 다른 컴포넌트가 밀리거나 이동하면 안 된다.** 레이아웃 시프트는 시청자에게 불안감을 준다.

### 금지 패턴

```
🔴 절대 금지:
- translateY/translateX로 요소가 이동하면서 형제 요소를 밀어내는 것
- flex/flow 안에서 크기 변하는 애니메이션 (다른 요소 위치 변경 유발)
- 조건부 렌더링 없이 항목 추가 (기존 항목 위치 변경)
- scale 애니메이션이 레이아웃에 영향을 주는 것
- 과도한 slideY/slideX (±40px 초과 금지)
```

### 안전한 애니메이션 패턴

```tsx
// ✅ 좋음: opacity만 변경 (레이아웃 영향 없음)
const opacity = interpolate(frame, [start, start + 15], [0, 1], {
  extrapolateLeft: "clamp", extrapolateRight: "clamp"
});

// ✅ 좋음: transform만 변경 (레이아웃 영향 없음)
// 단, 이동 범위는 ±30px 이내로 제한
const slideY = interpolate(frame, [start, start + 18], [20, 0], {
  extrapolateLeft: "clamp", extrapolateRight: "clamp"
});

// ✅ 좋음: absolute 위치에서 애니메이션 (다른 요소와 독립)
<div style={{ position: 'absolute', opacity, transform: `translateY(${slideY}px)` }}>

// ✅ 좋음: SVG 내부 애니메이션 (strokeDashoffset, fill 등 — 레이아웃 무관)
<circle strokeDashoffset={dashOffset} />

// ❌ 나쁨: flex 안에서 요소 크기/위치가 변하며 형제 밀어냄
<div className="flex flex-col gap-4">
  <div style={{ height: expanding ? 200 : 0 }} /> {/* 밀어냄! */}
  <div>이 요소가 아래로 밀림</div>
</div>

// ❌ 나쁨: 큰 translateY로 다른 요소 겹침
const slideY = interpolate(frame, [start, start + 20], [80, 0], ...); // 80px은 너무 큼
```

### 이동 거리 제한

| 애니메이션 종류 | 최대 이동 거리 | 비고 |
|----------------|---------------|------|
| slideY (등장) | **±30px** | 20px 권장 |
| slideX (등장) | **±30px** | 20px 권장 |
| scale (바운스) | **0.8 ~ 1.15** | 0.5→1.1→1 같은 극단적 스케일 금지 |
| rotate | **±5deg** | 미세한 회전만 |

### 항목 추가 시 안전한 패턴

```tsx
// ✅ 좋음: 각 항목이 absolute로 고정 위치
{items.map((item, i) => (
  <div key={i} style={{
    position: 'absolute',
    top: i * 80,
    left: 0,
    opacity: frame >= startFrame + i * 15 ? 1 : 0,
  }}>
    {item}
  </div>
))}

// ✅ 좋음: flex 안이지만 모든 항목 미리 렌더링, opacity만 변경
<div className="flex flex-col gap-4">
  {items.map((item, i) => {
    const itemOpacity = interpolate(
      frame, [startFrame + i * 15, startFrame + i * 15 + 12], [0, 1],
      { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
    );
    return (
      <div key={i} style={{ opacity: itemOpacity }}>
        {item}
      </div>
    );
  })}
</div>

// ❌ 나쁨: 조건부 렌더링으로 항목 추가 — 아래 항목이 밀림
<div className="flex flex-col gap-4">
  <div>항목 1</div>
  {frame >= 60 && <div>항목 2 (추가 시 아래 항목 밀림!)</div>}
  <div>이 항목이 밀림</div>
</div>
```

### 체크리스트 (레이아웃 안정성)

- [ ] 🔴 애니메이션 중 다른 요소 위치가 변하지 않는가?
- [ ] 🔴 translateY/X 이동 거리가 ±30px 이내인가?
- [ ] 🔴 scale이 0.8~1.15 범위인가?
- [ ] flex/flow 안에서 크기가 변하는 요소가 없는가?
- [ ] 항목 순차 등장 시 opacity만 변경하거나 absolute 사용했는가?
- [ ] 조건부 렌더링이 레이아웃 시프트를 유발하지 않는가?

---

## 🔴 인트로 세그먼트 규칙 — 반드시 진행/움직임 인포그래픽

**인트로(첫 세그먼트, 0~5초)도 정적 텍스트 + 도트로 끝내면 안 된다.** 인트로는 시청자에게 "지금 무엇이 시작되는지"를 시각적 모션으로 알리는 구간이다. 큰 텍스트만 보여주면 정적이고 지루해서 이탈 발생.

```
🔴 차단:
- 큰 타이틀 + 4 도트가 일자로 가만히 있는 패턴
- "STEP 1, 2, 3, 4" 텍스트 나열만
- IconReveal + 큰 텍스트 조합 (텍스트 주인공)

✅ 허용 (진행/모션 인포그래픽):
- 진행 화살표 로드맵 (좌→우 fillProgress, 마일스톤 순차 점등, 화살표 팁 펄스)
- 카운트다운/카운트업 (3 → 2 → 1 → START, 또는 0 → 4)
- 단계 카드가 한 장씩 쌓이며 등장
- 게이지/프로그레스가 차오름
- 다이얼/스피너가 회전해 시작 표시
```

```tsx
// ✅ 좋음 — 로드맵 진행 화살표 (인트로 패턴)
const lineProgress = interpolate(frame, [LINE_START, LINE_START + LINE_DURATION], [0, 1], ...);
const filledLineLength = lineProgress * ARROW_END_X;

<svg width={1240} height={220}>
  <line stroke="rgba(255,255,255,0.18)" strokeWidth={10} />        {/* 백그라운드 트랙 */}
  <line x2={filledLineLength} stroke="#00FF88" strokeWidth={10} /> {/* 그린 진행 */}
  {STEP_X.map((cx, i) => (                                          /* 라인 도달 시 점등 */
    <Milestone cx={cx} triggerFrame={triggerFrameForX(cx)} />
  ))}
  <polygon points="..." fill="#00FF88" />                           {/* 끝에서 화살표 팁 펄스 */}
</svg>

// ❌ 나쁨 — 정적 4 도트 + 큰 텍스트
<p className="text-9xl">AI와 일하는 4단계</p>
<div className="flex gap-10">
  {[1,2,3,4].map(n => <Circle number={n} />)}
</div>
```

**핵심**: 인트로는 데이터/시간/상태가 흐르는 모션이 보여야 한다. 정적 인포그래픽도 인트로에선 부족.

---

## 🔴 사람·생물 SVG 직접 그리지 마라 — 추상 메타포 사용

**사람, 동물, 로봇 등 생물 형상을 직접 SVG로 그리면 100% 어색하다.** 머리/몸/팔다리 비례를 SVG로 맞추기 어렵고, 검정 배경 + 단색이라 더더욱 부자연스러움. 대신 **추상 메타포**를 쓴다.

🔴 **손, 손가락도 사람 부위 — 금지 범위에 포함**. "결제/송금" 표현은 카드 + NFC ring 호로.
🔴 **월계관도 어려움 범위** — 잎사귀를 곡선 따라 정렬하기 어려워서 콩알처럼 떠 보임. 메달 배지로 대체.

| 표현하고 싶은 것 | ❌ 직접 SVG | ✅ 추상 메타포 |
|----------------|------------|--------------|
| AI / 병사 / 실행자 | 사람 머리+몸+군모 SVG | **체스 폰** (졸/병사 = 작은 말) |
| 사람 / 전략가 / 결정자 | 사람 머리+몸+왕관 SVG | **체스 킹** (왕관+십자가) |
| 사고/지능 | 뇌 SVG (주름, 곡선) | **전구**, **체스 킹**, **별/스파클** |
| 빠름 | 달리는 사람 | **번개**, **로켓**, **속도계 바늘** |
| 협업 | 손잡은 사람 둘 | **맞물린 톱니 2개**, **퍼즐 조각 2개** |
| 사용자 | 사람 머리 (얼굴) | **단순 원 + 텍스트 라벨** ("USER") |

```tsx
// ❌ 나쁨 — 사람 형상 SVG (어색함)
<svg viewBox="0 0 100 100">
  <path d="M 22 38 Q 50 12 78 38..." fill={color} />  {/* 군모 */}
  <circle cx={50} cy={56} r={12} fill={color} />       {/* 얼굴 */}
  <path d="M 30 70 ..." fill={color} />                {/* 몸 */}
</svg>

// ✅ 좋음 — 체스 폰 (병사/졸 = 추상 형상)
<svg viewBox="0 0 100 100">
  <circle cx={50} cy={20} r={11} fill={color} />
  <ellipse cx={50} cy={36} rx={14} ry={3.5} fill={color}/>
  <path d="M 39 40 L 32 74 L 68 74 L 61 40 Z" fill={color}/>
  <rect x={22} y={74} width={56} height={10} rx={2} fill={color}/>
  <ellipse cx={50} cy={86} rx={30} ry={4.5} fill={color}/>
</svg>

// ✅ 좋음 — 체스 킹 (전략가/왕)
<svg viewBox="0 0 100 100">
  <rect x={47} y={4} width={6} height={20} fill={color}/>           {/* 십자가 세로 */}
  <rect x={41} y={10} width={18} height={6} fill={color}/>          {/* 십자가 가로 */}
  <circle cx={50} cy={30} r={5} fill="none" stroke={color} strokeWidth={2.5}/>
  <path d="M 28 42 Q 28 35 36 33 L 64 33 ..." fill={color}/>        {/* 왕관 밴드 */}
</svg>
```

**원칙**:
1. 비교/대비 비유는 **체스말, 톱니, 다이얼, 도형**으로
2. 캐릭터(사람/동물)는 **단색 단일 도형(원, 사각형) + 라벨**로 대체
3. 의인화가 꼭 필요하면 **이모지** 사용 (단색 SVG보다 자연스러움)
4. SVG로 사람을 5분 이상 그리고 있다면 → 멈추고 메타포로 전환

---

## 감정/전환 시각 요소 (텍스트 대신 아이콘)

감정이나 전환 구간도 **반드시 SVG 아이콘/도형**과 함께. 텍스트만 쓰지 않는다.

```tsx
// ✅ 좋음: 놀라움 = 느낌표 SVG + 작은 라벨
<SafeContentArea>
  <svg width={156} height={156} viewBox="0 0 156 156">
    <circle cx={78} cy={78} r={72} fill="none" stroke="#00FF88" strokeWidth={5} />
    <rect x={70} y={32} width={16} height={65} rx={8} fill="#00FF88" />
    <circle cx={78} cy={117} r={9} fill="#00FF88" />
  </svg>
  <p className="text-3xl mt-4" style={{ color: 'rgba(255,255,255,0.7)' }}>놀라움</p>
</SafeContentArea>

// ✅ 좋음: 질문 = 물음표 SVG 도형
// ✅ 좋음: 시작 = 플레이 버튼 SVG 삼각형
// ✅ 좋음: 성공 = 체크마크 원형 SVG

// ❌ 차단: 텍스트만으로 감정 전달
<p className="text-8xl font-black">정말?</p>  // ← 금지
```

---

## SVG 아이콘 컴포넌트 패턴 (Icons.tsx)

### 🔴 아이콘 동반 규칙

**모든 텍스트 라벨/항목에 반드시 SVG 아이콘이 동반되어야 한다.** 텍스트만 덩그러니 놓는 것은 금지.

```
🔴 차단: 텍스트만 있는 항목 (아이콘 없음)
✅ 허용: SVG 아이콘 + 텍스트 라벨 조합
✅ 허용: 이모지 + 텍스트 라벨 (SVG가 불가한 경우)
```

### 공유 Icons.tsx 파일

컴포지션 폴더에 `Icons.tsx`를 만들어 SVG 아이콘 컴포넌트를 공유한다.

```tsx
// src/compositions/my-video/Icons.tsx
export const PersonIcon: React.FC<{ size?: number; color?: string }> = ({
  size = 80, color = "#FFFFFF",
}) => (
  <svg width={size} height={size} viewBox="0 0 80 80" fill="none">
    <circle cx="40" cy="20" r="14" fill={color} />
    <path d="M14 70 C14 48 66 48 66 70" fill={color} />
  </svg>
);

export const RobotIcon: React.FC<{ size?: number; color?: string }> = ({
  size = 80, color = "#00FF88",
}) => (
  <svg width={size} height={size} viewBox="0 0 80 80" fill="none">
    <rect x="16" y="24" width="48" height="40" rx="8" stroke={color} strokeWidth="4" />
    <circle cx="32" cy="44" r="6" fill={color} />
    <circle cx="48" cy="44" r="6" fill={color} />
    <line x1="40" y1="10" x2="40" y2="24" stroke={color} strokeWidth="3" />
    <circle cx="40" cy="8" r="4" fill={color} />
  </svg>
);
```

### 주요 아이콘 용도

| 아이콘 | 용도 | 색상 |
|--------|------|------|
| `PersonIcon` | 사람, 사용자, 인간 역할 | `#FFFFFF` 또는 `#00FF88` |
| `RobotIcon` | AI, 자동화, 기계 역할 | `#00FF88` |
| `CameraIcon` | 촬영, 카메라 작업 | `#FFFFFF` |
| `AlertIcon` | 경고, 주의, 반전 | `#00FF88` (맥락으로 부정 전달) |
| `TrashIcon` | 실패, 쓰레기, 나쁜 결과 | `#FFFFFF` (중립 — 텍스트로 부정 전달) |
| `BrainIcon` | 사고, 착각, 인식 | `rgba(255,255,255,0.7)` |
| `EditIcon` / `SubtitleIcon` / `PaletteIcon` | 편집 스킬 | `#FFFFFF` |
| `CheckIcon` | 성공, 완료, 확인 | `#00FF88` |
| `LightbulbIcon` | 아이디어, 영감 | `#00FF88` |
| `RocketIcon` | 성장, 가속, 생산성 | `#00FF88` |

### 아이콘 크기 기준

| 위치 | 크기 | 비고 |
|------|------|------|
| 세그먼트 인트로 (메인 아이콘) | 180-260px | 화면 중앙, 큰 임팩트 |
| 비교/좌우 분할 | 110-140px | 원형 컨테이너 안 |
| 프로세스 단계/워크플로우 | 56-64px | 라벨 옆 |
| 바 차트 하단 | 56-64px | 데이터 식별 |

---

## 🔴 배경 이미지 금지

**세그먼트에 배경 이미지(Img + staticFile)를 사용하지 않는다.** 저 opacity(0.15~0.3)로 깔아도 인포그래픽을 가리고 가독성을 떨어뜨린다.

```
🔴 차단: <Img src={staticFile("images/...")} style={{ opacity: 0.2 }} />
🔴 차단: 배경 이미지 위에 텍스트/아이콘 오버레이
✅ 허용: 순수 검정 배경 (#000000) + SVG 인포그래픽
✅ 허용: 미세한 비네팅/그라디언트 (검정 범위 내)
```

**이유**: 검정 배경에서 SVG 아이콘 + 텍스트가 가장 선명하고 가독성이 높다. 배경 이미지는 아무리 투명도를 낮춰도 인포그래픽과 시각적으로 충돌한다.

---

## 🔴 제품/브랜드 로고 규칙 — 실제 로고 사용 필수

**스크립트에서 제품(GPT, Cursor, Figma, Notion 등)이 언급되면, 반드시 실제 로고를 사용한다.**
플레이스홀더 SVG나 텍스트만으로 제품을 표현하지 않는다.

### 워크플로우

1. **`public/images/logos/` 폴더 먼저 확인** — 이미 있으면 바로 사용
2. 없으면 웹에서 공식 로고 SVG/PNG 검색 후 폴더에 저장
3. Remotion `Img` + `staticFile`로 사용 (반드시 두 개 import):

```tsx
import { Img, staticFile } from "remotion";

<Img
  src={staticFile("images/logos/cursor.svg")}
  style={{ width: 120, height: 120 }}
/>
```

### 로고 파일 규칙

```
✅ SVG 우선 (해상도 무관, 깨끗)
✅ PNG은 최소 256x256px (작으면 깨짐)
✅ 투명 배경 (검정 배경 위에 올리므로) — 단, 브랜드 배경색 있는 로고 예외
❌ JPEG (투명 배경 불가)
❌ 텍스트만으로 제품 표현 ("Cursor"라고 글자만 쓰는 것 금지)
```

### 🔴 배경색 있는 로고 처리 (Claude, ChatGPT 등)

**일부 로고(Claude orange, ChatGPT 등)는 자체 배경색을 가진다.** 이런 로고는 `borderRadius`로 브랜드 형태를 살려준다. 투명 배경 버전을 억지로 찾지 않는다.

```tsx
// ✅ claude.svg — 오렌지 배경, borderRadius로 라운드 처리
<Img
  src={staticFile("images/logos/claude.svg")}
  style={{ width: 56, height: 56, borderRadius: 12 }}
/>

// ✅ 큰 히어로 사이즈
<Img
  src={staticFile("images/logos/claude.svg")}
  style={{ width: 120, height: 120, borderRadius: 24 }}
/>

// ❌ 나쁨: borderRadius 없이 → 각진 사각형이 어색하게 보임
<Img src={staticFile("images/logos/claude.svg")} style={{ width: 56, height: 56 }} />
```

**borderRadius 가이드:**
| 크기 | borderRadius |
|------|-------------|
| 40–56px | 10–12px |
| 64–80px | 14–16px |
| 100–120px | 20–24px |
| 160px+ | 28–32px |

### 로고 인디케이터 패턴 — UI 요소 안 삽입

**로고는 히어로 아이콘 용도 외에, UI 요소 안 "인디케이터"로도 사용한다.**
"누가 이 작업을 하는가"를 시각적으로 명시할 때 유용.

```tsx
// ✅ 타이틀 옆 — "클로드가 자동으로 기록한다"는 맥락 강화
<div className="flex items-center gap-5">
  <Img
    src={staticFile("images/logos/claude.svg")}
    style={{ width: 64, height: 64, borderRadius: 14 }}
  />
  <p className="text-6xl font-black" style={{ color: '#00FF88' }}>자동 작업 기록</p>
</div>

// ✅ 박스/카드 내부 — "이 공간에서 클로드가 작업 중"
<div style={{ border: '4px solid #00FF88', borderRadius: 16, padding: 24 }}>
  <Img
    src={staticFile("images/logos/claude.svg")}
    style={{ width: 48, height: 48, borderRadius: 10 }}
  />
  <p style={{ color: '#00FF88' }}>작업 공간 A</p>
</div>

// ✅ 라벨 옆 — 질문/대화 출처 표시
<div className="flex items-center gap-4">
  <Img
    src={staticFile("images/logos/claude.svg")}
    style={{ width: 52, height: 52, borderRadius: 12 }}
  />
  <p className="text-4xl font-bold" style={{ color: '#00FF88' }}>클로드가 질문을 던져요</p>
</div>
```

**인디케이터 크기 기준:**
| 사용 위치 | 크기 |
|---------|------|
| 타이틀 옆 (큰 텍스트) | 56–72px |
| 라벨/소제목 옆 | 44–56px |
| 박스/카드 내부 | 40–52px |
| 체크포인트/타임라인 | 32–40px |

### 로고 표시 패턴 (히어로)

```tsx
// ✅ 좋음: IconReveal + 실제 로고 + 라벨
<IconReveal startFrame={20} size={200}>
  <Img src={staticFile("images/logos/cursor.svg")} style={{ width: 160, height: 160 }} />
</IconReveal>
<p className="text-4xl font-bold mt-4" style={{ color: '#FFFFFF' }}>Cursor</p>

// ❌ 나쁨: 텍스트만
<p className="text-7xl font-black">Cursor</p>

// ❌ 나쁨: 손으로 그린 SVG로 대체 (실제 로고 있으면 금지)
<svg width={48} height={48}>
  <circle cx="24" cy="24" r="20" stroke="#00FF88" strokeWidth={3} />
  <text fill="#00FF88">C</text>
</svg>
```

---

## 🔴 이미지/로고 중앙 렌더링 규칙

**모든 이미지와 로고는 반드시 화면 중앙에 렌더링되어야 한다.** SafeContentArea를 사용하거나, absolute 중앙 정렬 패턴을 사용한다.

```tsx
// ✅ 좋음: SafeContentArea 안에서 중앙 정렬
<SafeContentArea>
  <div className="flex flex-col items-center justify-center gap-6">
    <Img src={staticFile("images/logos/figma.svg")} style={{ width: 200, height: 200 }} />
    <p className="text-4xl font-bold" style={{ color: '#FFFFFF' }}>Figma</p>
  </div>
</SafeContentArea>

// ✅ 좋음: absolute 중앙 정렬
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
  <div className="absolute inset-0 flex items-center justify-center" style={{ paddingBottom: 230 }}>
    <Img src={staticFile("images/logos/notion.png")} style={{ width: 180, height: 180 }} />
  </div>
</AbsoluteFill>

// ❌ 나쁨: 좌측 정렬되거나 상단에 붙어있는 이미지
<div style={{ marginLeft: 100, marginTop: 50 }}>
  <Img src={...} />
</div>
```

---

## 핵심 패턴

### SafeContentArea (필수)

```tsx
const SafeContentArea: React.FC<{ children: React.ReactNode }> = ({ children }) => (
  <AbsoluteFill style={{ backgroundColor: '#000000' }}>
    <div
      className="absolute inset-0 flex flex-col items-center justify-center px-20"
      style={{ paddingBottom: 230 }}
    >
      {children}
    </div>
  </AbsoluteFill>
);
```

### 다중 Phase 레이아웃 (필수 — 중앙 정렬 유지)

세그먼트에 phase가 2개 이상이면, **반드시 각 phase를 독립적인 absolute 컨테이너로 분리**.

```tsx
// ✅ 좋음: 각 phase가 독립적으로 중앙 정렬됨
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
  {/* Phase 1: 인포그래픽 */}
  <div
    className="absolute inset-0 flex flex-col items-center justify-center px-20"
    style={{ paddingTop: 80, paddingBottom: 230, opacity: phase1Opacity }}
  >
    <ComparisonBar ... />
  </div>

  {/* Phase 2: 결과 */}
  <div
    className="absolute inset-0 flex flex-col items-center justify-center px-20"
    style={{ paddingTop: 80, paddingBottom: 230, opacity: phase2Opacity }}
  >
    <AnimatedCounter ... />
  </div>
</AbsoluteFill>

// ❌ 나쁨: 같은 flex column — opacity 0이어도 공간 차지
```

**인포그래픽 점진적 빌드업**: 나중에 등장하는 항목은 조건부 렌더링으로 DOM에서 제외:

```tsx
{frame >= startFrame - 15 && (
  <ComparisonBar label="시간" ... />
)}
```

### SegmentWrapper (씬 전환)

```tsx
const SegmentWrapper: React.FC<{
  children: React.ReactNode;
  fadeInFrames?: number;
  fadeOutFrames?: number;
}> = ({ children, fadeInFrames = 15, fadeOutFrames = 15 }) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();

  const opacity = Math.min(
    interpolate(frame, [0, fadeInFrames], [0, 1], { extrapolateRight: "clamp" }),
    interpolate(frame, [durationInFrames - fadeOutFrames, durationInFrames], [1, 0], { extrapolateLeft: "clamp" })
  );

  return <AbsoluteFill style={{ opacity }}>{children}</AbsoluteFill>;
};
```

### 색상 상수

```tsx
const COLORS = {
  bg: '#000000',
  text: '#FFFFFF',
  accent: '#00FF88',
} as const;
```

---

## 트랜스크립션 워크플로우

OPENAI_API_KEY는 .env 파일에 있으니 사용하세요

### Whisper API

```bash
curl https://api.openai.com/v1/audio/transcriptions \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -F file="@audio.mp3" \
  -F model="whisper-1" \
  -F response_format="verbose_json" \
  -F "timestamp_granularities[]=word" \
  -o transcript.json
```

### 자막 데이터 (CaptionLine 형식)

Whisper API로 트랜스크립트 생성 후 CaptionLine 형식으로 변환. 자막은 **배경 없이 흰색 텍스트만** 표시.
자세한 구현은 [CAPTION_SAFE_AREA.md](CAPTION_SAFE_AREA.md) 참조.

```typescript
import type { CaptionLine } from "./PodcastCaption";

// 모든 타임스탬프는 AUDIO 시간 (Whisper 원본)
export const CAPTIONS: CaptionLine[] = [
  {
    text: "안녕하세요 여러분",
    start: 0.0,
    end: 1.5,
    words: [
      { text: "안녕하세요", start: 0.0, end: 0.8 },
      { text: "여러분", start: 0.8, end: 1.5 },
    ],
  },
];
```

**규칙:**
- AI가 직접 transcript 분석 후 자막 생성
- Whisper 한국어 분리 오류 수정 (예: "1호는" → "이론은")
- 분리된 단어 합치기 (예: "보도" + "자료를" → "보도자료를")
- 2초 미만 짧은 자막은 합침
- `whitespace-nowrap` 필수
- 🔴 타임스탬프는 반드시 **오디오 시간** (PLAYBACK_RATE 변환은 컴포넌트에서)
- 🔴 자막 스타일: 배경 박스 없음, 그라데이션 없음, 흰색 font-black 텍스트 + textShadow만

---

## 애니메이션 타이밍

```typescript
// 자막 타이밍 → 프레임 계산
// 세그먼트 시작: 22.8초, 대사 시작: 29.36초
// 프레임 = (29.36 - 22.8) * 60fps = 394

const opacity = interpolate(frame, [370, 394], [0, 1], {
  extrapolateLeft: "clamp",
  extrapolateRight: "clamp"
});
```

---

## 세그먼트 디자인 결정 트리

새 세그먼트를 만들 때 이 순서로 결정 — **인포그래픽 우선**:

```
대사 내용 분석
├─ 핵심 수치 2-4개 동시? → 🟢 MetricCard Grid (패턴 16) — paired 카드
├─ 성취/돌파/1위/0건? → 🟢 AchievementBadge (패턴 19) — 월계관/방패/깃발
├─ 인트로 + 큰 핵심수치? → 🟢 미니멀 인트로 (패턴 20) — inline 강조 + paired hero
├─ 숫자/데이터 단일? → AnimatedCounter + MetricCard
├─ 비교? → ComparisonBar 또는 SplitComparison
├─ 비율/퍼센트? → DonutChart (600px+) + 중앙 숫자
├─ 달성률/점수/완성도? → DonutChart 또는 ComparisonBar
├─ 전환율/단계별 감소? → FunnelChart (1000px+ 너비)
├─ 프로세스/단계? → ProcessStep 순차 빌드업
├─ 시간 흐름/역사? → Timeline 가로/세로
├─ 관계/연결? → NodeMap + AnimatedLine
├─ 도구/기술 소개? → IconReveal (200px+) + Logo + 라벨
├─ 구조/아키텍처? → Diagram (Mermaid/D2)
├─ 중심+주변 관계? → RadialLayout (800x600px+ 영역)
├─ Before/After? → SplitComparison 좌우 분할
├─ 트레이드오프? → TradeoffPanel (패턴 15)
├─ 감정/전환/질문? → SVG 아이콘 (260px+) + 미니멀 라벨
└─ 위 모두 해당 없음? → SVG 기본 도형 (원/삼각형/화살표) + 라벨
```

🟢 = 미니멀 스타일 패턴. 모든 hero icon에 **3D depth** (그라데이션 + drop shadow, 패턴 17) + **sparkle 장식** (패턴 18) 적용. 자세히 [MINIMAL_DESIGN_LANGUAGE.md](MINIMAL_DESIGN_LANGUAGE.md) 참조.

**🔴 핵심 원칙**: 텍스트만으로 세그먼트를 구성하는 것은 **금지**다. "시각화 불가능"은 존재하지 않는다. 느낌표/물음표/화살표/원/삼각형 — 기본 도형만으로도 시각화는 가능하다.

---

## 렌더링 (사용자 직접 실행 — AI는 실행 금지)

> **🔴 AI는 아래 명령어를 실행하지 않는다. 사용자에게 안내만 한다.**

### GPU 가속 렌더링 (권장)

```bash
pnpm exec remotion render MyVideo \
  --output out/video.mp4 \
  --codec h264 \
  --crf 16 \
  --audio-codec aac \
  --audio-bitrate 192K \
  --gl=angle \
  --concurrency=8
```

### 스틸 이미지로 디자인 확인

```bash
pnpm exec remotion still <CompositionId> --frame 100
```

---

## 디자인 체크리스트

### 코딩 전
- [ ] 대사 내용 분석 → 세그먼트별 **시각화 방법** 결정
- [ ] 인포그래픽 패턴 매칭 (결정 트리 활용)
- [ ] 텍스트만 쓰는 세그먼트는 감정/전환 구간뿐인지 확인

### 인포그래픽 우선
- [ ] 숫자 → AnimatedCounter 또는 MetricCard
- [ ] 비교 → ComparisonBar 또는 SplitComparison
- [ ] 비율 → DonutChart
- [ ] 프로세스 → ProcessStep
- [ ] 시간 → Timeline
- [ ] 관계 → NodeMap / RadialLayout / Diagram
- [ ] 기술 → IconReveal + Logo

### 레이아웃
- [ ] 화면 여백 50% 이상 (인포그래픽도 여유있게)
- [ ] 한 프레임에 시각적 메시지 1개
- [ ] 각 phase가 독립적인 `absolute inset-0` 컨테이너
- [ ] 점진적 항목 → 조건부 렌더링 `{frame >= X && (...)}`
- [ ] 🔴 인포그래픽 ↔ 라벨/임팩트 텍스트 간격 최소 60px (권장 70-80px)
- [ ] 🔴 상단 카테고리 라벨 ↔ 메인 인포그래픽 간격 최소 48px (권장 60-70px)
- [ ] Phase 안 텍스트 그룹과 다른 그룹 사이 최소 60px
- [ ] 인포-텍스트 간격에 `<div style={{ height: 60+ }} />` spacer 또는 marginTop 60+ 사용

### 색상 (3색만 — 예외 없음)
- [ ] `#000000` — 배경
- [ ] `#FFFFFF` — 텍스트 / 선 / 대조 대상
- [ ] `#00FF88` — 유일한 강조색 (모든 강조, 긍정, 핵심, 데이터)
- [ ] 🔴 **#FFD700 (골드) 사용 안 했는가?** → 삭제된 색
- [ ] 🔴 **#FF3232 (레드) 사용 안 했는가?** → 삭제된 색
- [ ] 다른 색 사용 없음 (Tailwind 색상 클래스, 보라/파랑/주황 등 전부 금지)
- [ ] 🔴 회색/반투명(0.4, 0.5) 텍스트 없는가? (최소 0.7 또는 #FFFFFF)

### 모션 & 레이아웃 안정성
- [ ] 순차 빌드업 (하나씩 등장, 동시 금지)
- [ ] 18-30 프레임 애니메이션 (빠르고 스냅핑)
- [ ] ease-out 커브 (자연스러운 감속)
- [ ] 차트/그래프는 30-40 프레임 (데이터가 그려지는 느낌)
- [ ] 🔴 translateY/X 이동 거리 ±30px 이내인가?
- [ ] 🔴 scale 범위 0.8~1.15인가?
- [ ] 🔴 애니메이션 중 다른 요소 위치가 변하지 않는가?
- [ ] flex 안 항목 순차 등장 시 opacity만 변경했는가?

### 가시성 (검정 배경 대비)
- [ ] 🔴 모든 SVG stroke/fill이 밝은 색인가? (#FFFFFF, #00FF88만 허용)
- [ ] 🔴 선 strokeWidth가 최소 3px인가?
- [ ] 🔴 도형 border가 최소 3px인가?
- [ ] 🔴 어두운 fill (#333, #1a1a1a 등) 사용 안 했는가?
- [ ] 빈 트랙 배경이 rgba(255,255,255,0.1) 이상인가?

### AI 슬롭 방지
- [ ] 🔴 **게이지/스피드미터(GaugeMeter) 금지** — 반원형 진행률 차트는 대시보드 느낌이라 영상에 안 맞음. DonutChart, ComparisonBar, VerticalBar 등으로 대체
- [ ] 🔴 **가짜 수치 박지 마라** — `₩1,000,000`, `+73%`, `10x faster` 같은 발명한 숫자는 시청자에게 거짓말. 스크립트에 있는 수치만 OK
- [ ] 🔴 **수직 막대 차트 maxHeight ≤ 400px** — 1080p 안전 영역 850px 안에 라벨/제목/임팩트 텍스트까지 다 들어가야 함
- [ ] 🔴 **메인 아이콘 ≥ 320px** — 200px는 장식 수준. 단일 아이콘 메인이면 320~360px 필수
- [ ] 🔴 **펄스 링/동심원 ripple 애니메이션 사용 안 했는가?** — 무한 반복 ripple은 어지러움 유발. 정적 구간은 순차 빌드업 / 카운터 / 차트 grow 등으로 채울 것
- [ ] 🔴 **차트 막대 사이 dashed connector 화살표 없는가?** — SVG 좌표 정합 어려워서 무조건 어색. 변화량은 하단 callout 텍스트("절반으로 ↓")로 표현
- [ ] 🔴 **자막과 애니메이션 트리거 sync 검증** — 모든 reveal trigger 프레임이 해당 자막의 audio start ±20 frames 안에 있는가? `frame = audioSec / playbackRate * fps`
- [ ] 🔴 **연속 모션 — 0.5초 이상 정적 구간 없는가?** — 인포그래픽 reveal 사이 갭이 크면 스켈레톤(빈 outline) 먼저 등장 + 자막 sync 시 콘텐츠 reveal, 또는 진행형 칩으로 채우기
- [ ] 보라색/다른 hue 그라데이션 금지 (palette 내 같은 hue gradient는 미니멀 depth용 허용)
- [ ] 의미 없는 그리드 레이아웃 금지 (단, 미니멀 paired metric card grid는 의미적 묶음이므로 허용 — 패턴 16)
- [ ] rounded-3xl 과도 사용 금지 (단, 미니멀 paired metric card 안에서는 허용)
- [ ] 이모지 플로팅/파티클 금지
- [ ] 얇은 보더 박스 무의미 나열 금지 (단, 미니멀 paired card는 4px 보더 + 의미 있는 KPI 묶음 허용)
- [ ] 🟢 미니멀 depth icon에 gradient + drop shadow 적용했는가? (평평 단색 SVG는 영상 카드에서 빈약)
- [ ] 🟢 sparkle 장식 3-5개만 사용했는가? (루프 금지)
- [ ] 🔴 텍스트만으로 세그먼트 구성 = 자동 FAIL (인포그래픽 필수!)
- [ ] 🔴 text-7xl 이상 텍스트가 화면 주인공 = 자동 FAIL
- [ ] 🔴 감정/전환도 SVG 아이콘 필수 (텍스트만 금지)
- [ ] 🔴 인트로가 진행/모션 인포그래픽인가? (정적 텍스트+도트 = 자동 FAIL)
- [ ] 🔴 사람·동물·로봇 SVG를 직접 그리지 않았는가? (체스말/도형/이모지로 대체)
- [ ] 🔴 모든 큰 텍스트(fontSize 40+)에 lineHeight: 1.1 명시했는가?
- [ ] 🔴 콘텐츠 총 높이 770px 이하인가? (1080 - 80 paddingTop - 230 paddingBottom)

---

## 문제 해결

| 문제 | 원인 | 해결 |
|------|------|------|
| 자막에 콘텐츠가 가림 | 하단 포지셔닝 | SafeContentArea 사용 |
| 색상이 잘못됨 | Tailwind 클래스 사용 | 인라인 스타일 + 헥스 코드 사용 |
| 전환이 부자연스러움 | 페이드 없음 | SegmentWrapper 사용 |
| 자막 줄바꿈 | whitespace-nowrap 없음 | 클래스 추가 |
| SIGTRAP 에러 | 브라우저 문제 | `--gl=angle` 사용 |
| 숫자가 안 움직임 | interpolate 누락 | AnimatedCounter 사용 |
| SVG 안 보임 | viewBox 누락 | viewBox 설정 + 고정 w/h 제거 |
| 차트가 끊김 | 프레임 수 부족 | 최소 30 프레임 차트 애니메이션 |
| 콘텐츠 중앙 안 맞음 | 여러 phase가 flex column에 | 별도 `absolute inset-0`으로 분리 |
| STEP 뱃지/타이틀이 위쪽에서 짤림 | paddingTop 누락 | safe area에 `paddingTop: 80` 추가 + lineHeight 1.1 명시 |
| 콘텐츠가 자막 영역에 가까움 | 콘텐츠 height > 770px | fontSize 한 단계 ↓, 히어로 아이콘/그리드 행 수 ↓ |
| 사람/동물 SVG가 어색함 | 직접 형상 그림 | 체스말, 도형, 이모지 메타포로 교체 |
| 인포그래픽 밀림 | 숨겨진 요소가 공간 차지 | 조건부 렌더링 `{frame >= X && (...)}` |
| 다이어그램 안 렌더링 | Diagram import 누락 | `../../components/Diagram`에서 import |
| 타임라인 항목 겹침 | 간격 너무 좁음 | gap 늘리거나 항목 수 줄이기 |
| **상단 제목 잘림 / 임팩트 텍스트가 자막과 겹침** | **차트 maxHeight 너무 큼** | **차트 maxHeight 400px 이하로 축소, 안전 영역 850px 합산 확인** |
| **3초+ 정적 화면이 지루** | **자막 흐르는 동안 비주얼 변화 없음** | **카드 내부 콘텐츠 순차 빌드업, 카운터, 차트 grow 추가 (펄스 링은 어지러움 유발 — 금지)** |
| **자막과 애니메이션이 어긋남 ("싱크 안 맞음")** | **trigger 프레임을 막연히 정함** | **`frame = audioSec / PLAYBACK_RATE * 60` 공식으로 모든 trigger 정확히 계산 (기본 PLAYBACK_RATE=1.0). 각 자막 start 프레임 ±20 안에 정렬** |
| **인포그래픽 reveal 사이 빈 시간이 큼 ("애니메이션이 중간중간 비어")** | **trigger 간격이 너무 멀어 죽은 구간** | **(1) 스켈레톤 outline을 일찍 등장 + 자막 sync 시 reveal, (2) 4-칩 진행형 패턴, (3) 두 reveal 사이에 마이크로 이벤트 (label slide, glow ramp) 삽입** |
| **막대 사이 dashed 화살표가 어색** | **SVG 좌표가 막대 top과 안 맞음 + 비스듬한 점선이 허공을 지나감** | **dashed connector 제거 → 하단 callout 텍스트("절반으로 ↓")로 변화량 표현** |
| **카운터에 박은 ₩/% 가 시청자에게 거짓말** | **스크립트에 없는 수치를 발명** | **VerticalBar 추세 비교(단위 없음) 또는 코인/아이콘 양 비교로 대체** |
| **단일 아이콘이 화면에서 빈약** | **아이콘 200px 이하** | **320~360px로 확대 + 펄스 링 추가** |

---

## 🔴 멀티-Phase 세그먼트 구조 규칙 (필수)

세그먼트 안에 여러 Phase (A/B/C/D) 가 있을 때 발생하는 흔한 버그들과 해결법.

### 1. SafeContentArea는 opacity prop을 받아야 한다

**나쁜 패턴 (블리딩 발생):**

```tsx
<SegmentWrapper>
  <SafeContentArea>  {/* 기본 bg #000000 + opacity 없음 */}
    <div style={{ opacity: phaseAOp }}>...</div>
  </SafeContentArea>
  <SafeContentArea>
    <div style={{ opacity: phaseBOp }}>...</div>
  </SafeContentArea>
</SegmentWrapper>
```

문제: 각 `SafeContentArea`는 `AbsoluteFill` + 검정 bg. DOM 순서상 **마지막 SafeContentArea의 bg가 모든 이전 phase 위를 항상 덮음**. Phase A 콘텐츠가 절대 안 보임 (Phase D bg가 가림). **자막만 보이는 검정 화면**의 원인.

**옳은 패턴:**

```tsx
// SegmentWrapper.tsx
export const SafeContentArea = ({ children, opacity = 1, paddingTop = 90 }) => (
  <AbsoluteFill style={{ backgroundColor: "#000000", opacity }}>
    <div style={{ position:"absolute", inset:0, padding... }}>{children}</div>
  </AbsoluteFill>
);

// 세그먼트 사용
<SegmentWrapper>
  <SafeContentArea opacity={phaseAOp}>...</SafeContentArea>
  <SafeContentArea opacity={phaseBOp}>...</SafeContentArea>
</SegmentWrapper>
```

이렇게 하면 Phase 활성일 때만 bg 보임. 크로스페이드 자연스러움.

### 2. SegmentWrapper는 fadeInFrames=0 지원해야 한다

첫 세그먼트 + 인트로는 frame 0부터 즉시 보여야 함. SegmentWrapper의 fadeIn 15 프레임이 시작 0.25s 검정 만듦 → 자막 흐르는데 인포그래픽 안 보임.

```tsx
export const SegmentWrapper = ({ fadeInFrames = 15, fadeOutFrames = 15, children }) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const inOp =
    fadeInFrames <= 0
      ? 1
      : interpolate(frame, [0, fadeInFrames], [0, 1], { extrapolateRight:"clamp" });
  const outOp = fadeOutFrames <= 0 ? 1 : interpolate(...);
  return <AbsoluteFill style={{ opacity: Math.min(inOp, outOp) }}>{children}</AbsoluteFill>;
};
```

호출 시: `<SegmentWrapper fadeInFrames={0}>` 첫 프레임부터 콘텐츠 가시.

⚠️ `interpolate(frame, [0, 0], [0, 1])` 직접 사용 시 "input range must be strictly monotonically increasing" 에러 — 위 패턴처럼 분기로 처리.

### 3. 헤더/인트로 요소는 opacity=1로 시작

```tsx
// ❌ 헤더가 fadeIn(frame, aToF(0.4), 18) 으로 시작 → 0.4s 지연
const headerOp = fadeIn(frame, aToF(0.4), 18);

// ✅ 즉시 가시
const headerOp = 1;
// 또는
const headerOp = fadeIn(frame, 0, 6);
```

Icon entrance도 동일: `<HookIcon startFrame={-30} />` 처럼 음수 startFrame으로 frame 0에 이미 entrance 완료.

---

## 🔴 SVG 좌표계 통일 (wrapper coord)

### 문제: SVG viewBox vs HTML wrapper coord 불일치

SafeContentArea inner div: padding 80/80/90/230 적용 → **wrapper 실제 사이즈 = 1760×760** (1920-160 가로, 1080-320 세로). HTML `position:absolute` 자식들은 wrapper coord (0-1760 가로, 0-760 세로) 사용.

SVG가 `viewBox="0 0 1920 1080"` + `position:absolute, inset:0` + 기본 `preserveAspectRatio="xMidYMid meet"` 사용 시:

- SVG element 픽셀 사이즈 = 1760×760 (wrapper에 맞춰짐)
- SVG 콘텐츠 (1920×1080 coord) → 0.704 스케일로 fit (작은 dim 기준) → 1351×760로 렌더링, 가로 204px씩 padding으로 centered
- **SVG x=960 (SVG 콘텐츠 가운데)** → wrapper 픽셀 **x=880** 으로 렌더링
- HTML `left:960` (wrapper coord) → wrapper 픽셀 **x=960**
- **80px 어긋남** → 연결선이 카드/말 위치를 빗나감

### 해결: SVG viewBox를 wrapper와 매칭

```tsx
<svg
  width="100%"
  height="100%"
  viewBox="0 0 1760 760"        // wrapper와 1:1
  preserveAspectRatio="none"     // stretching 허용
  style={{ position:"absolute", inset:0 }}
>
  {/* SVG x/y는 wrapper coord와 동일 */}
  <line x1={880} y1={380} x2={1520} y2={620} ... />
</svg>
```

그리고 HTML 요소도 wrapper coord 사용 (대칭 좌표):

```tsx
const CENTER_X = 880;   // wrapper center (NOT 960)
const CENTER_Y = 380;   // wrapper mid Y (NOT 480)

<HTMLElement style={{ left: 880, top: 380 }} />
<svg ... ><line x1={880} y1={380} ... /></svg>
```

### 자식 위치 계산

5개 요소 가로 균등 배치 (wrapper 1760 기준):

```tsx
// 옳음 — wrapper coord 명시
const positions = [240, 580, 880, 1180, 1520];
positions.map(x => <div style={{ position:"absolute", left:x, transform:"translate(-50%, 0)" }}>...</div>);

// SVG broadcast line endpoint도 동일 좌표
positions.map(x => <line x1={880} x2={x} ... />);
```

flex `justifyContent:center` + gap 사용하면 컴파일된 위치가 wrapper coord와 일치한다 보장 없음 → **명시적 absolute + 미리 계산된 left 사용**.

---

## 🔴 카드/요소 사이즈 매칭

좌우 비교 카드는 콘텐츠 양이 달라도 시각적으로 동일 사이즈여야 한다.

```tsx
// ❌ 콘텐츠로만 사이즈 결정 — 오른쪽 카드 카운터 때문에 길어짐
<div style={{ padding: 40 }}>왼쪽 짧은 내용</div>
<div style={{ padding: 40 }}>
  오른쪽 카운터 + 라벨 (자동 height ↑)
</div>

// ✅ 명시적 height + 중앙 정렬
<div style={{ height: 380, padding: 30, justifyContent: "center" }}>왼쪽</div>
<div style={{ height: 380, padding: 30, justifyContent: "center" }}>오른쪽</div>
```

---

## 🔴 z-order: 트랙 line이 마일스톤 원을 통과하면 안 됨

같은 Y 좌표에 SVG line과 HTML milestone 원이 있으면, milestone 원의 반투명 bg는 트랙 line이 비쳐 보임 → 트랙이 원을 "관통"하는 듯한 시각 버그.

```tsx
// ❌ 반투명 bg
<div style={{ backgroundColor: "rgba(0,255,136,0.12)", border: "4px solid #00FF88" }}>

// ✅ solid bg로 트랙 가림
<div style={{ backgroundColor: "#000000", border: "4px solid #00FF88" }}>
```

---

## 🔴 차트 X축 라벨과 다른 요소 충돌 방지

차트 SVG 안 "시간 →" / "AI 능력" 같은 axis 라벨 위치 + 추가 요소 (anchor box, label) 영역 분리.

```
❌ axis 라벨과 다른 요소가 같은 X 범위 + 같은 Y 범위에 동시 존재
✅ 라벨은 corner 한정 (예: bottom-right 코너)
✅ 다른 요소는 axis 영역 밖 위치
```

차트 + 부가 visualization (anchor, chain) 충돌하면 부가 visualization 자체를 제거하는 게 낫다. 단순함 우선.

---

## 🔴 SVG size 비례 텍스트 (재사용 컴포넌트)

CalendarDial 등 size prop 받는 SVG 컴포넌트의 내부 텍스트 fontSize는 **size 비례**로 작성.

```tsx
// ❌ 고정 fontSize (size=240일 때 viewBox 밖으로 튀어나감)
<text fontSize={34}>점검 주기</text>
<text fontSize={44}>3 ~ 6 개월</text>

// ✅ size 비례
<text fontSize={Math.round(size * 0.09)}>점검 주기</text>
<text fontSize={Math.round(size * 0.14)}>3~6개월</text>
```

위치도 비례:
```tsx
<text y={cy - size * 0.05}>...</text>
<text y={cy + size * 0.10}>...</text>
```

---

## 🔴 콘텐츠 wrapper bounds 엄수

wrapper = 1760×760. `top:860` 같은 값은 자막 영역(viewport y=920+) 침범 → 자막에 가려짐.

```
✅ top 범위: 0 ~ 700 (요소 height 고려)
❌ top:800+ — 자막 영역 진입
✅ bottom 범위: 30 ~ 700
```

배너/임팩트 텍스트:
```tsx
<div style={{ position:"absolute", bottom: 30 }}>...</div>  // 자막 위 안전
```

---

## 🔴 인트로에 모션 인포그래픽 필수 (정적 텍스트+도트 = FAIL)

여러 세그먼트가 동일 시리즈 (1/3, 2/3, 3/3 같은) 인트로를 공유할 때, 정적 step indicator (3개의 사각형) + 큰 텍스트만으로 끝내면 안 됨. **진행/모션 인포그래픽 필요**.

해결: 공통 **Roadmap 컴포넌트** — 좌→우 fillProgress, 마일스톤 순차 점등, 현재 단계 강조, 완료 단계 체크마크.

```tsx
// segments/Roadmap.tsx
export const Roadmap = ({ currentStep, trackStartFrame, labels }) => {
  // 진행 fill 0→targetFill (currentStep별)
  // 마일스톤 3개: 이전 = ✓ accent, 현재 = number + 펄스 ring, 미래 = number dim
  return <svg viewBox="0 0 1920 260"><line ... /><circle ... /></svg>;
};

// 각 세그먼트
<Roadmap currentStep={1} trackStartFrame={0} labels={["정기 점검", "DRI", "매니저"]} />
<Roadmap currentStep={2} ... />
<Roadmap currentStep={3} ... />
```

배경 트랙은 항상 가시 (`stroke="rgba(255,255,255,0.18)"`), 진행 fill만 애니메이션.

---

## 🔴 SegmentWrapper의 모든 자식이 즉시 보여야 함

세그먼트 진입 시 다음 항목들이 **frame 0부터 가시 (opacity 1)**:

- 헤더 텍스트 + 메인 아이콘
- 백그라운드 track / 빈 outline / 스켈레톤
- (선택) 초기 카테고리 라벨

세부 데이터/마일스톤만 자막 sync에 맞춰 fade in.

체크리스트:
- [ ] `<SegmentWrapper fadeInFrames={0}>` 인지?
- [ ] 헤더 `opacity = 1` 또는 `fadeIn(frame, 0, 6)` 인지?
- [ ] Icon `startFrame={-30}` 또는 음수로 즉시 표시?
- [ ] Roadmap/track 백그라운드가 frame 0에 보이는지?

---

## 관련 파일

- `src/presets.ts` — 비디오 프리셋
- `src/config.ts` — 프레임 유틸리티
- `src/components/Caption.tsx` — 자막 타입
- `src/components/Logo.tsx` — 로고 컴포넌트 (스프링 애니메이션)
- `src/components/Diagram.tsx` — Mermaid/D2 다이어그램 렌더러
- `src/components/DiagramSlide.tsx` — 다이어그램 슬라이드 래퍼
