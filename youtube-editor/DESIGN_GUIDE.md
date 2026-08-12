# 프론트엔드 디자인 가이드

Remotion 비디오 컴포지션을 위한 **인포그래픽 퍼스트** 디자인 가이드.

---

## 디자인 철학: 인포그래픽 퍼스트

### 핵심 원칙

1. **그림이 곧 설명이다** — 텍스트로 설명하지 마라, 그려서 보여줘라
2. **한 프레임, 한 인포그래픽** — 하나의 시각적 메시지에 집중
3. **데이터를 시각화하라** — 숫자, 비교, 프로세스는 반드시 차트/다이어그램으로
4. **움직임이 이해를 돕는다** — 순차 빌드업으로 정보를 전달
5. **텍스트는 라벨이다** — 텍스트는 그림의 보조, 주인공이 아니다
6. **여백이 시각물을 살린다** — 인포그래픽도 화면의 50%+ 여백 유지

### 금지 패턴

```
❌ 텍스트만으로 세그먼트 구성 (인포그래픽으로 대체 가능한 경우)
❌ 보라색/다른 hue 그라데이션
❌ 이모지 플로팅/파티클
❌ 3개 이상 컬러 동시 사용
❌ 그리드 레이아웃으로 무의미 정보 나열 (단, paired metric card는 허용 — 패턴 16)
❌ 얇은 보더 박스
❌ 그라데이션 배경 (palette 밖 hue)
❌ absolute bottom-XX (자막 가림)
❌ 큰 텍스트만 띡 — 시각화 없이 text-9xl만 쓰는 건 최후의 수단
```

### 🟢 미니멀 스타일 허용 (palette 내부에서)

```
✅ paired metric card grid (2-up, 3-up, 2x2) — 의미 있는 KPI 묶음
✅ rounded-2xl/3xl 카드 — paired metric 안에서만
✅ palette 내 같은 hue gradient (#00FF88 → #00B85F)
✅ palette 내 drop shadow (rgba(0,255,136,0.3), rgba(255,255,255,0.1), rgba(0,0,0,0.5))
✅ radial highlight (rgba(255,255,255,0.4) → 0) — 3D 입체감용
✅ 4-point sparkle 장식 (3-5개, hero 주변, 루프 없음)
✅ 월계관/방패/깃발 메타포 (성취/안전/순위 표현)
```

자세히 [MINIMAL_DESIGN_LANGUAGE.md](MINIMAL_DESIGN_LANGUAGE.md) 참조.

---

## 인포그래픽 타입 선택

### 결정 트리 (세그먼트 설계 시 필수)

```
대사 내용 분석
├─ 숫자/데이터가 있나? ──────→ AnimatedCounter + MetricCard
├─ 비교가 핵심인가? ──────→ ComparisonBar / SplitComparison
├─ 비율/퍼센트인가? ──────→ DonutChart + 중앙 숫자
├─ 단계/프로세스인가? ──────→ ProcessStep 순차 빌드업
├─ 시간 흐름/역사인가? ──────→ Timeline (가로/세로)
├─ 관계/연결을 보여주나? ──→ NodeMap + AnimatedLine
├─ 도구/기술 소개인가? ──────→ IconReveal + Logo
├─ 구조/아키텍처인가? ──────→ Diagram (Mermaid/D2)
├─ 중심+주변 관계인가? ──────→ RadialLayout
├─ Before/After인가? ──────→ SplitComparison
├─ 핵심 수치 나열인가? ──────→ MetricCard 쌓기
├─ 달성률/점수/완성도? ──────→ GaugeMeter (500px+ 반원형)
├─ 전환율/단계별 감소? ──────→ FunnelChart (1000px+ 너비)
├─ 감정 전환/질문인가? ──────→ SVG 아이콘 (260px+) + 미니멀 라벨
└─ 위 모두 해당 없음? ──────→ SVG 기본 도형 + 라벨
```

**원칙**: 텍스트만으로 세그먼트를 채우는 건 마지막 수단. 항상 "이걸 그림으로 보여줄 수 없을까?" 먼저 생각.

### 인포그래픽 분류

| 카테고리 | 패턴 | 사용 시점 |
|----------|------|-----------|
| **데이터** | AnimatedCounter, MetricCard | 숫자, 통계, KPI |
| **비교** | ComparisonBar, SplitComparison | A vs B, 장단점 |
| **비율** | DonutChart (600px+) | 비율, 퍼센트 |
| **달성률** | GaugeMeter (500px+) | 점수, 완성도, 진행률 |
| **전환율** | FunnelChart (1000px+) | 단계별 감소, 전환율 |
| **프로세스** | ProcessStep, Timeline | 단계, 순서, 역사 |
| **관계** | NodeMap, RadialLayout, Diagram | 연결, 구조, 의존성 |
| **정체성** | IconReveal, Logo | 브랜드, 기술, 도구 |
| **드로잉** | AnimatedLine | 화살표, 연결선, 강조선 |

---

## 🔴 인포그래픽 가시성: 검정 배경에서 잘 보여야 한다

배경은 항상 `#000000`이다. 모든 인포그래픽(SVG, 차트, 도형, 선, 아이콘)은 검정 위에서 **확실히 눈에 띄어야** 한다.

### 색상 규칙
- SVG stroke/fill → 밝은 색만: `#FFFFFF`, `#00FF88` (3색 팔레트 준수)
- 어두운 fill 금지: `#333`, `#1a1a1a`, `#222` 등은 검정과 구분 안 됨

### 두께 규칙
- 선(line/path) strokeWidth: **최소 3px** (1~2px은 검정에서 사라짐)
- 도형 border: **최소 3px**
- 연결선/가이드선: **최소 strokeWidth 3**

### 배경 트랙 규칙
- 도넛 빈 부분, 프로그레스 바 빈 부분: `rgba(255,255,255,0.1)` 이상
- 연결선 가이드: `rgba(255,255,255,0.15)` 이상
- `0.05`는 안 보인다 — 최소 `0.1`

```tsx
// ✅ 좋음
<circle stroke="#00FF88" strokeWidth={5} />
<line stroke="#FFFFFF" strokeWidth={3} />
<rect fill="#00FF88" />

// ❌ 나쁨 — 안 보임
<circle stroke="rgba(255,255,255,0.3)" strokeWidth={1} />
<rect fill="#333333" />
<div style={{ backgroundColor: '#1a1a1a' }} />
```

---

## 타이포그래피: 라벨 역할만 (텍스트는 절대 주인공이 아니다)

### 🔴 무관용 원칙: 텍스트 주도 세그먼트 = 자동 FAIL

```
모든 세그먼트는 반드시 SVG/차트/도형/아이콘이 시각적 주인공이어야 한다.
텍스트는 ONLY 라벨, 값 표시, 카테고리명으로만 사용.
text-7xl 이상 텍스트가 화면 중앙을 차지하면 = FAIL
"감정 전환"도 SVG 아이콘(느낌표, 물음표, 체크마크) + 라벨로 처리
```

### 🔴 글씨색 규칙: 회색/반투명 텍스트 금지

검정 배경에서 회색 글씨는 안 보인다. 모든 텍스트는 잘 보이는 색만 사용.

```
❌ rgba(255,255,255,0.4)  // 너무 어둡다 — 금지
❌ rgba(255,255,255,0.5)  // 너무 어둡다 — 금지
❌ opacity: 0.4 on text   // 글자에 적용 금지
❌ text-gray-xxx          // Tailwind 회색 금지
✅ #FFFFFF                // 기본 텍스트
✅ rgba(255,255,255,0.7)  // 보조 라벨 (허용 최소치)
✅ #00FF88                // 유일한 강조색
```

### 🔴 한글 우선 규칙

**모든 텍스트는 반드시 한글로 작성한다.** 영어 라벨, 영어 카테고리명 금지. 고유명사(GPT, AI, Cursor 등)만 영어 허용.

```
❌ "COST COMPARISON" → ✅ "비용 비교"
❌ "SAVED TIME" → ✅ "절약한 시간"
❌ "EDIT CYCLE" → ✅ "편집 주기"
✅ "GPT-3", "ChatGPT", "Cursor" (고유명사는 영어 OK)
```

### 🔴 글자 크기 규칙 — 크게 넣어라

1920x1080에서 글자가 작으면 안 보인다. **항상 한 단계 크게** 넣어라.

| 용도 | 최소 크기 | 권장 크기 |
|------|----------|----------|
| 히어로 카운터 | text-8xl (96px) | **text-9xl (128px)** |
| 데이터 값 | text-7xl (72px) | **text-8xl (96px)** |
| 항목 라벨 | text-4xl (36px) | **text-5xl (48px)** |
| 비교 헤더 | text-5xl (48px) | **text-6xl (60px)** |
| 카테고리 라벨 | text-3xl (30px) | **text-4xl (36px)** |
| 보조 설명 | text-2xl (24px) | **text-3xl (30px)** |

### 허용되는 텍스트 역할

```tsx
// ✅ 카테고리 라벨 (rgba 0.7 또는 #FFFFFF — 한글, 크게)
text-4xl tracking-widest           // 36px — 섹션 제목 (한글)
text-3xl font-medium               // 30px — 보조 설명 (한글)

// ✅ 데이터 값 (차트/카운터 내부의 숫자만)
text-9xl font-black tabular-nums   // 128px — 카운터 숫자 (차트 안에만)
text-8xl font-black tabular-nums   // 96px — 보조 값 (차트 안에만)

// ✅ 항목 라벨 (차트 옆 짧은 이름 — 한글)
text-5xl font-bold                 // 48px — 바 차트 라벨
```

### 🔴 차단되는 텍스트 패턴

```tsx
// ❌ 절대 금지: 텍스트가 세그먼트의 메인 콘텐츠
<p className="text-9xl font-black">미친거 하나</p>        // 차단
<p className="text-8xl font-black">시작해볼까요?</p>      // 차단
<p className="text-7xl font-black">만족스러운 퀄리티</p>   // 차단

// ❌ 절대 금지: 영어 라벨
<p>COST COMPARISON</p>  // 차단 → "비용 비교"
<p>SAVED TIME</p>       // 차단 → "절약한 시간"

// ❌ 절대 금지: 2줄 이상 텍스트가 세그먼트 전부
<p className="text-5xl">AI로 홈페이지 만든다고?</p>
<p className="text-6xl">퀄리티가 나오겠어?</p>            // 차단

// ❌ 절대 금지: 큰 글자 + 작은 글자 typographic hierarchy
<p className="text-5xl" style={{ opacity: 0.4 }}>오늘</p>
<p className="text-9xl font-black">미친거 하나</p>        // 차단
```

---

## 공간 구성

### 레이아웃 규칙

- **인포그래픽 중심 정렬** — 시각물이 화면 중앙에 위치
- **여백 50%+ 확보** — 인포그래픽도 여유 있게
- **한 포컬 포인트** — 차트 하나, 다이어그램 하나 (단, paired metric card는 의미적 묶음이므로 예외)
- **라벨은 위에** — 인포그래픽 위에 작은 카테고리 라벨
- **다중 Phase 분리 필수** — phase 전환 시 독립 absolute 컨테이너
- **조건부 렌더링** — 점진적 빌드업 항목은 `{frame >= X && (...)}` 패턴
- **미니멀 paired card** — 2-up/3-up grid 사용 시 카드끼리 stagger 8-12 프레임 등장 + 하나만 accent

### 구성 예시

```tsx
// 타입 1: 데이터 — 카운터 + 라벨
<SafeContentArea>
  <div className="text-center">
    <p className="text-3xl tracking-widest uppercase"
       style={{ color: 'rgba(255,255,255,0.7)' }}>
      절약한 시간
    </p>
    <AnimatedCounter value={127} suffix="시간" startFrame={20} color="#00FF88" />
  </div>
</SafeContentArea>

// 타입 2: 비교 — 막대 차트
<SafeContentArea>
  <div className="w-full max-w-4xl flex flex-col gap-8">
    <ComparisonBar label="외주" value={500} maxValue={500} color="#FFFFFF" startFrame={20} index={0} />
    <ComparisonBar label="바이브" value={50} maxValue={500} color="#00FF88" startFrame={20} index={1} />
  </div>
</SafeContentArea>

// 타입 3: 프로세스 — 순차 스텝
<SafeContentArea>
  <div className="flex flex-col gap-6">
    <ProcessStep number={1} label="아이디어" startFrame={20} isActive={false} />
    <ProcessStep number={2} label="프롬프트" startFrame={20} isActive={true} />
    <ProcessStep number={3} label="배포" startFrame={20} isActive={false} />
  </div>
</SafeContentArea>

// 타입 4: 관계도 — 방사형
<SafeContentArea>
  <RadialLayout center="AI" items={[...]} />
</SafeContentArea>

// 타입 5: 감정/전환 — SVG 아이콘 + 라벨 (텍스트만 금지)
<SafeContentArea>
  <svg width={150} height={150} viewBox="0 0 150 150">
    <circle cx={75} cy={75} r={65} fill="none" stroke="#00FF88" strokeWidth={4} />
    <text x={75} y={95} textAnchor="middle" fontSize={80} fontWeight={900} fill="#00FF88">?</text>
  </svg>
  <p className="text-3xl mt-4" style={{ color: 'rgba(255,255,255,0.7)' }}>질문</p>
</SafeContentArea>
```

---

## 모션 디자인

### 원칙

1. **순차 빌드업** — 인포그래픽 요소는 하나씩 나타난다 (동시 금지)
2. **데이터가 그려진다** — 막대가 자라고, 숫자가 카운팅되고, 선이 그려진다
3. **스냅핑** — 18-30프레임 안에 완료 (느리적느리적 금지)
4. **ease-out** — 자연스러운 감속 (linear 금지)
5. **의미 있는 방향** — 막대: 좌→우, 숫자: 0→N, 선: 시작점→끝점

### 애니메이션 타이밍 참조

| 애니메이션 타입 | 지속 시간 | 항목 간 딜레이 |
|----------------|----------|---------------|
| 페이드 인 | 15-20 프레임 | — |
| 슬라이드 + 페이드 | 20-25 프레임 | — |
| 스케일 바운스 | 25 프레임 | — |
| **카운터** | **30-40 프레임** | — |
| **막대 성장** | **30 프레임** | **20 프레임** |
| **도넛 호** | **40 프레임** | — |
| **선 그리기** | **30 프레임** | — |
| **타임라인 점** | **20 프레임** | **25 프레임** |
| 항목 스태거 | 각 20 프레임 | 15-18 프레임 |
| 씬 페이드 인/아웃 | 15-20 프레임 | — |

### 표준 이징

```tsx
// 막대/차트 성장 (가장 자주 사용)
const barWidth = interpolate(frame, [start, start + 30], [0, targetWidth], {
  extrapolateLeft: "clamp", extrapolateRight: "clamp"
});

// 카운터 (ease-out cubic)
const eased = 1 - Math.pow(1 - progress, 3);
const current = Math.round(eased * value);

// SVG 선 그리기 (strokeDashoffset 감소)
const progress = interpolate(frame, [start, start + 30], [length, 0], {
  extrapolateLeft: "clamp", extrapolateRight: "clamp"
});

// 스케일 바운스 (등장)
const scale = interpolate(
  frame, [start, start + 15, start + 25], [0.5, 1.1, 1],
  { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
);
```

---

## 인포그래픽 크기 (1920x1080 기준)

### 🔴 최소 크기 규칙

1920x1080 해상도에서 인포그래픽이 너무 작으면 영상에서 안 보인다. 아래 최소 크기를 반드시 준수.

### SVG 아이콘/도형 크기

| 요소 | 최소 크기 | 권장 크기 | 용도 |
|------|----------|----------|------|
| **메인 SVG** (세그먼트 주인공) | 300px | 360-420px | 물음표, 체크마크, 기어, 화살표 |
| **비교 아이콘** (좌우 비교) | 120px | 140-160px | X마크, 체크마크 원형 |
| **목록 항목 아이콘** (체크리스트) | 64px | 72-80px | X마크, 불릿 아이콘 |
| **작은 인디케이터** (커서, 화살표) | 56px | 64-72px | 클릭 커서, 방향 화살표 |

### 컨테이너/막대 크기

| 요소 | 최소 | 권장 | 비고 |
|------|------|------|------|
| **프롬프트/터미널 바** | width: 900px | 1000-1200px | 타이핑 애니메이션 바 |
| **프로그레스 바 높이** | h-12 (48px) | h-14 (56px) | 게이지, 프로그레스 |
| **비교 막대 높이** | h-16 (64px) | h-20 (80px) | 좌우 비교 바 |
| **막대 컨테이너 최대 너비** | max-w-3xl | max-w-4xl~5xl | 데이터 바 래퍼 |
| **분할 비교 너비** | max-w-1000 | max-w-1200 | 좌우 분할 비교 |
| **도넛/원형 차트** | 500px | 600px | 반드시 화면의 40%+ |
| **게이지/스피드미터** | 500px | 600px | 반원형 진행률 |
| **펀넬 차트 너비** | 1000px | 1100px | 단계별 감소 차트 |

### 카운터/숫자 크기

| 요소 | 최소 | 권장 | 비고 |
|------|------|------|------|
| **히어로 카운터** | fontSize: 120px | 140-160px | 메인 숫자 카운터 |
| **데이터 값** | text-7xl (72px) | text-8xl~9xl | 차트 내 숫자 |
| **라벨 텍스트** | text-2xl (24px) | text-3xl (30px) | 바 옆 라벨 |

### PPT/이미지 표시

| 요소 | 최소 | 권장 | 비고 |
|------|------|------|------|
| **전체화면 이미지** | width: 1200px | 1400px | 한 장씩 보여줄 때 |
| **카드 이미지** | width: 600px | 700-800px | 카드 형태 |

### 크기 조절 팁

SVG viewBox는 그대로 두고 width/height만 키우면 비율 유지하면서 크기 조절 가능:

```tsx
// ✅ viewBox 유지, 렌더 크기만 키움
<svg width={260} height={260} viewBox="0 0 180 180">
  {/* 기존 SVG 내용 그대로 */}
</svg>

// ❌ viewBox도 바꾸면 내부 좌표 다 깨짐
<svg width={260} height={260} viewBox="0 0 260 260">
  {/* 좌표 다시 계산해야 함 */}
</svg>
```

### 🔴 화면 점유율 규칙

인포그래픽(SVG, 차트, 도형)이 콘텐츠 안전 영역의 **최소 40%** 를 차지해야 한다.

```
🔴 절대 규칙:
- 메인 인포그래픽 최소: 300px (너비 또는 높이)
- 도넛/원형: 최소 500px (권장 600px)
- 비교 바 전체 너비: 최소 1000px
- 단일 SVG 메인: 최소 300px (권장 360px)
- "장식" 수준의 작은 인포그래픽은 점수에서 +2점만 (화면 40%+ = +4점)
```

---

## 🔴 레이아웃 쏠림 금지: 화면 중앙 정렬 원칙

### 자주 발생하는 쏠림 패턴 (모두 금지)

#### 패턴 1: 작은 아이콘 왼쪽 + 목록 오른쪽 (빈 공간 발생)
```tsx
// ❌ 나쁨: 아이콘이 작고 왼쪽에 치우침, 오른쪽 화면이 텅 빔
<div style={{ display: "flex", gap: 80, alignItems: "center" }}>
  <div style={{ flexShrink: 0 }}>
    <svg width={100} height={100} ... />  {/* 너무 작음! */}
    <p>회사 조직도</p>
  </div>
  <div style={{ flex: 1, flexDirection: "column" }}>
    <Item text="CEO" />
    <Item text="CTO" />
    ...
  </div>  {/* 오른쪽 절반이 비어버림 */}
</div>

// ✅ 좋음: 중앙 정렬 + 큰 SVG 위 + 칩 아래
<div className="absolute inset-0 flex flex-col items-center justify-center gap-8">
  <svg width={500} height={200} ... />  {/* 인포그래픽이 주인공 */}
  <p>회사 조직도</p>
  <div style={{ display: "flex", flexWrap: "wrap", gap: 16, justifyContent: "center" }}>
    <Chip text="CEO" />
    <Chip text="CTO" />
  </div>
</div>
```

#### 패턴 2: alignSelf: flex-start 로 상단/좌측 쏠림
```tsx
// ❌ 나쁨: 라벨이 좌상단에 붙고 나머지 화면이 비어버림
<p style={{ alignSelf: "flex-start" }}>개리텐이 직접 이렇게 말했어요</p>
<div style={{ alignSelf: "flex-end" }}>— 핵심이라고요</div>

// ✅ 좋음: 중앙 정렬 유지
<p>개리텐이 직접 이렇게 말했어요</p>  {/* 부모의 items-center 상속 */}
<p style={{ color: "#00FF88" }}>— 핵심이라고요</p>
```

#### 패턴 3: 조건부 렌더링으로 레이아웃 시프트
```tsx
// ❌ 나쁨: frame >= X 마다 DOM 추가 → 다른 항목 밀림
{frame >= 792 && <OrgItem text="CEO" />}
{frame >= 840 && <OrgItem text="CTO" />}

// ✅ 좋음: 항상 렌더링 + opacity만 변경 (공간 고정)
<OrgChip text="CEO" startFrame={792} />  {/* opacity 0→1 애니메이션 내부 처리 */}
<OrgChip text="CTO" startFrame={840} />
```

### 목록 항목을 표시하는 올바른 패턴

항목 나열은 **bullet list 금지**, **flex-wrap 칩** 또는 **카드**로:
```tsx
// ❌ 나쁨: 불릿 리스트 (작고 왼쪽 쏠림)
<div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
  <div style={{ display: "flex", alignItems: "center", gap: 16 }}>
    <div style={{ width: 8, height: 8, borderRadius: "50%", backgroundColor: "rgba(255,255,255,0.3)" }} />
    <p style={{ color: "#FFFFFF", fontSize: 42 }}>CEO</p>
  </div>
</div>

// ✅ 좋음: flex-wrap 칩 (중앙 정렬, 크고 잘 보임)
<div style={{ display: "flex", flexWrap: "wrap", gap: 16, justifyContent: "center" }}>
  <div style={{ padding: "18px 40px", borderRadius: 48, border: "3px solid rgba(255,255,255,0.22)" }}>
    <p style={{ color: "#FFFFFF", fontSize: 46, fontWeight: 800 }}>CEO</p>
  </div>
</div>

// ✅ 좋음: 번호 카드 (내용에 순서가 있을 때)
<div style={{ display: "flex", flexDirection: "column", gap: 20, width: "100%" }}>
  <div style={{ display: "flex", alignItems: "center", gap: 28, padding: "22px 36px",
    borderRadius: 18, border: "3px solid rgba(255,255,255,0.18)" }}>
    <div style={{ width: 72, height: 72, borderRadius: "50%", border: "3px solid #FFFFFF" }}>
      <p style={{ color: "#FFFFFF", fontSize: 30, fontWeight: 900 }}>1</p>
    </div>
    <p style={{ color: "#FFFFFF", fontSize: 48, fontWeight: 800 }}>제품을 다시 생각하는 CEO</p>
  </div>
</div>
```

### 인포그래픽 크기 체크: "아이콘이 너무 작지 않은가?"

세그먼트를 완성한 후 반드시 확인:
```
❌ SVG width 100px 이하 → 작아서 장식처럼 보임 → 최소 300px으로
❌ 아이콘 옆에 목록 나열 → 아이콘이 조연으로 전락 → 아이콘을 위에, 목록을 아래에
❌ 화면 좌절반만 콘텐츠, 우절반 검정 빈 공간 → 중앙 정렬 + flex-wrap으로 해결
✅ SVG가 화면 너비의 40%+ 차지 (1920x1080 → 최소 768px)
✅ 모든 콘텐츠가 items-center justify-center 부모 안에 있음
✅ 목록 항목이 flex-wrap 칩/카드로 중앙 배치됨
```

---

## 🔴 레이아웃 안정성: 레이아웃 시프트 금지

애니메이션으로 인해 다른 컴포넌트가 밀리거나 이동하면 안 된다.

### 이동 거리 제한

| 종류 | 최대 | 권장 |
|------|------|------|
| translateY/X | ±30px | ±20px |
| scale | 0.8~1.15 | 0.9~1.05 |
| rotate | ±5deg | ±3deg |

### 규칙

```
🔴 금지:
- flex/flow 안에서 크기가 변하는 애니메이션 (형제 요소 밀어냄)
- 조건부 렌더링으로 flex 안에 항목 추가 (아래 항목 밀림)
- translateY 40px+ (너무 큰 이동)
- scale 0.5→1.1→1 (극단적 스케일 바운스)

✅ 안전:
- opacity만 변경 (레이아웃 무관)
- transform: translateY(20px→0) (작은 이동, 레이아웃 무관)
- SVG 내부 속성 변경 (strokeDashoffset 등)
- absolute 위치에서 애니메이션 (독립적)
- flex 안 모든 항목 미리 렌더링 → opacity만 변경
```

---

## 배경 & 분위기

### 🔴 배경 이미지 금지

배경 이미지(`<Img>` + `staticFile`)를 세그먼트에 사용하지 않는다. 저 opacity로 깔아도 인포그래픽 가독성을 떨어뜨린다.

```
🔴 <Img src={staticFile("...")} style={{ opacity: 0.2 }} />  // 금지
✅ 순수 검정 배경 + SVG 인포그래픽  // 가장 선명
```

### 허용

```tsx
// 순수 검정 (기본)
style={{ backgroundColor: '#000000' }}

// 미세한 비네팅 (차트 강조)
background: 'radial-gradient(ellipse at center, rgba(255,255,255,0.03) 0%, #000000 70%)'

// 아주 미세한 수직 그라디언트
background: 'linear-gradient(180deg, #000000 0%, #0a0a0a 100%)'

// 🟢 미니멀 스타일 카드 배경 (palette 내 미세 색)
style={{ backgroundColor: 'rgba(255,255,255,0.03)' }}  // 중립 카드
style={{ backgroundColor: 'rgba(0,255,136,0.06)' }}    // accent 카드
```

### 금지

```tsx
❌ background: 'linear-gradient(to right, #8B5CF6, #6366F1)'  // 보라색
❌ 패턴 배경, 노이즈 텍스처, 점 패턴
❌ bg-gray-900, bg-slate-800 등 Tailwind 클래스
```

---

## 품질 체크리스트

### 세그먼트별 검토

- [ ] 🔴 **텍스트만 세그먼트 = FAIL** — 모든 세그먼트에 SVG/차트/도형이 있는가?
- [ ] 🔴 **text-7xl 이상이 화면 주인공이면 = FAIL**
- [ ] 인포그래픽 패턴이 내용에 적합한가? (결정 트리 참조)
- [ ] 화면 여백 50% 이상인가?
- [ ] 한 프레임에 시각적 메시지 하나인가?
- [ ] 3색 팔레트만 사용했는가? (#000000, #FFFFFF, #00FF88 — 골드/레드 삭제됨)
- [ ] 애니메이션이 순차적인가? (동시 등장 없음)
- [ ] 차트/그래프가 "그려지는" 애니메이션인가? (snap 등장이 아닌)
- [ ] SafeContentArea 안에 들어가는가?
- [ ] 자막과 겹치지 않는가?
- [ ] 텍스트는 라벨/값 역할만 하는가? (주인공이 아닌)
- [ ] 🔴 회색/반투명(0.4, 0.5) 텍스트 없는가? (최소 0.7 또는 #FFFFFF)
- [ ] 🔴 SVG stroke/fill이 밝은 색인가? (검정 배경 대비 확인)
- [ ] 🔴 선 strokeWidth 최소 3px, border 최소 3px인가?
- [ ] 🔴 어두운 fill (#333, #1a1a1a) 사용 안 했는가?
- [ ] 🔴 **인포그래픽이 화면의 40%+ 차지하는가?** (장식이 아닌 주인공)
- [ ] 🔴 **translateY/X ±30px 이내인가?** (레이아웃 시프트 방지)
- [ ] 🔴 **scale 0.8~1.15 범위인가?**
- [ ] 🔴 **flex 안 항목 순차 등장 시 opacity만 변경했는가?**
