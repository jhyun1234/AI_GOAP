# 레이아웃 & 추가 패턴 규칙

SKILL.md의 보충 자료. 트레이드오프 비교 패널 패턴과 수직 공간 계산 규칙을 다룬다.

---

## 패턴 15: 트레이드오프 비교 패널 (이분법 cause→effect)

**언제 사용:** "A를 선택하면 X 비용, B를 선택하면 Y 비용" 같은 트레이드오프/딜레마. 삼각형/방사형 노드맵보다 좌우 비교 패널이 훨씬 명확.

### 🔴 트레이드오프는 삼각형 X, 좌우 비교 패널 ✅

```
❌ 나쁨: 3노드 삼각형 + 양방향 화살표 + floating 라벨
   → 시청자가 "원인/결과" 추적하기 어려움
   → 라벨이 노드/타이틀과 겹치는 문제 빈발

✅ 좋음: 좌우 패널 + 각 패널마다 ↓ 화살표 + 워닝 칩 체인
   → "이 옵션 → 이 결과" 인과 관계가 즉시 명확
   → 두 옵션의 비용을 동시에 비교 가능
```

### 구조

```tsx
// 좌우 두 패널 (FIXED height 필수 — 안전 영역 보장)
<div style={{ display: "flex", gap: 32 }}>
  <TradeoffPanel unitLabel="유닛 크게" visual={<BigBlockSVG />}>
    <DownArrow />
    <ChainStep label="테스트 불안정" />
    <DownArrow />
    <ChainStep label="동작 줄여야" />
  </TradeoffPanel>

  <TradeoffPanel unitLabel="유닛 작게" visual={<SmallBlocksSVG />}>
    <DownArrow />
    <ChainStep label="모킹 늘어남" />
    <MockBadgesRow count={5} />  {/* 시각적 "많아짐" 강조 */}
  </TradeoffPanel>
</div>
```

### 핵심 컴포넌트

```tsx
// ChainStep: 흰색 보더 + ⚠ 아이콘 + 라벨 (비용/단점 강조)
const ChainStep: React.FC<{
  label: string;
  startFrame: number;
}> = ({ label, startFrame }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [startFrame, startFrame + 14], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const scale = interpolate(
    frame, [startFrame, startFrame + 12, startFrame + 22], [0.85, 1.06, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <div className="flex items-center gap-3 rounded-2xl"
         style={{
           opacity, transform: `scale(${scale})`,
           padding: "12px 24px",
           backgroundColor: "rgba(255,255,255,0.06)",
           border: "3px solid #FFFFFF",
         }}>
      <svg width={32} height={32} viewBox="0 0 36 36">
        <path d="M 18 4 L 32 30 L 4 30 Z" stroke="#FFFFFF" strokeWidth={3}
              fill="none" strokeLinejoin="round" />
        <line x1={18} y1={14} x2={18} y2={22} stroke="#FFFFFF" strokeWidth={3}
              strokeLinecap="round" />
        <circle cx={18} cy={26} r={2} fill="#FFFFFF" />
      </svg>
      <p className="font-black" style={{ color: "#FFFFFF", fontSize: 32 }}>
        {label}
      </p>
    </div>
  );
};

// DownArrow: 인과 체인 사이의 ↓ 화살표 (작게)
const DownArrow: React.FC<{ startFrame: number }> = ({ startFrame }) => {
  const frame = useCurrentFrame();
  const progress = interpolate(frame, [startFrame, startFrame + 22], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <svg width={42} height={48} viewBox="0 0 42 48">
      <line x1={21} y1={4} x2={21} y2={32}
            stroke="rgba(255,255,255,0.6)" strokeWidth={4}
            strokeDasharray={28} strokeDashoffset={28 * (1 - progress)}
            strokeLinecap="round" />
      <path d="M 10 28 L 21 42 L 32 28" stroke="rgba(255,255,255,0.6)"
            strokeWidth={4} fill="none" strokeLinecap="round" strokeLinejoin="round"
            opacity={progress} />
    </svg>
  );
};

// TradeoffPanel: FIXED height (minHeight 금지)
const TradeoffPanel: React.FC<{
  unitLabel: string;
  visual: React.ReactNode;
  startFrame: number;
  children: React.ReactNode;
}> = ({ unitLabel, visual, startFrame, children }) => {
  const frame = useCurrentFrame();
  const opacity = interpolate(frame, [startFrame, startFrame + 16], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const slideY = interpolate(frame, [startFrame, startFrame + 22], [22, 0],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <div style={{
      opacity, transform: `translateY(${slideY}px)`,
      padding: 28,
      width: 700, height: 640,  // 🔴 FIXED — 안전 영역 보장
      border: `4px solid rgba(255,255,255,0.2)`,
      backgroundColor: "rgba(255,255,255,0.02)",
      borderRadius: 24,
      display: "flex", flexDirection: "column",
      alignItems: "center", gap: 14,
    }}>
      <p className="font-black" style={{ color: "#00FF88", fontSize: 56 }}>
        {unitLabel}
      </p>
      {visual}
      {children}
    </div>
  );
};
```

### 핵심 원칙

1. **옵션 라벨**은 패널 상단에 큰 글씨(text-5xl+ accent color)
2. **시각 차별화**: 두 옵션의 본질적 차이를 시각으로 (단일 큰 블록 vs 분할된 작은 블록들)
3. **인과 체인**: 옵션 → ↓ → 결과1 → ↓ → 결과2 (수직 흐름)
4. **워닝 칩**: 흰색 보더 + ⚠ 아이콘으로 "비용/단점" 명시
5. **시간 매핑**: 각 결과는 나레이션 시점에 맞춰 등장
6. **두 패널 동등 크기**: width/height 동일 (공정한 비교 인상)

### 활용 예

- 결정의 트레이드오프 (예: 유닛 크기, 추상화 레벨)
- A vs B 도구/기술 비교 (장단점 동시)
- "이쪽 비용 vs 저쪽 비용" 딜레마

---

## 🔴 수직 공간 계산 규칙 — 안전 영역 내 보장

**1080px 화면에서 사용 가능한 수직 공간을 정확히 계산하지 않으면 패널이 영역을 벗어나 타이틀/결론과 겹친다.**

### 사용 가능한 수직 공간

```
1080px (화면 높이)
- 70px (상단 안전 패딩)
- 230px (자막 영역 + 하단 안전 패딩)
─────────────────────────────────
= 780px (콘텐츠가 사용 가능한 수직 공간)
```

**이 780px 안에 모든 것(타이틀 + 메인 인포그래픽 + 결론)이 들어가야 한다.**

### 🔴 minHeight 사용 금지 — fixed height 사용

```tsx
// ❌ 절대 금지: minHeight (콘텐츠 영역 초과 시 overflow)
<div style={{ minHeight: 720 }}>
  {/* flex items-center일 때 위아래로 overflow → 타이틀과 겹침 */}
  {content}
</div>

// ✅ 좋음: fixed height (영역 보장)
<div style={{ height: 640 }}>     // 780 안에 안전하게 들어감
  {content}
</div>
```

### 레이아웃 패턴 — 단일 flex column

여러 요소(타이틀 + 메인 + 결론)를 동시에 보여줄 때, **각각을 absolute로 따로 두지 말고 단일 flex column으로 묶어 자동 공간 분배**:

```tsx
// ❌ 나쁨: absolute 분리 — 사이즈 변하면 겹침
<div style={{ position: "absolute", top: 70 }}>{title}</div>
<div style={{ position: "absolute", inset: 0, paddingTop: 130 }}>{panels}</div>
<div style={{ position: "absolute", bottom: 230 }}>{conclusion}</div>

// ✅ 좋음: 단일 flex column — 자동 spacing
<div style={{
  position: "absolute",
  top: 70, left: 0, right: 0, bottom: 230,  // 안전 영역 정확히 명시
  display: "flex",
  flexDirection: "column",
  alignItems: "center",
  justifyContent: "center",
  gap: 32,
}}>
  {title}      {/* 자동 배치 */}
  {panels}     {/* gap으로 자동 spacing */}
  {conclusion} {/* 충돌 없음 */}
</div>
```

### 패널 크기 가이드 (1080p, 자막 영역 230 기준)

| 콘텐츠 종류 | 권장 height |
|------------|------------|
| 단일 패널 (대형 인포그래픽 1개) | ≤ 720px |
| 좌우 비교 패널 (각각) | ≤ 640px |
| 타이틀 + 패널 + 결론 (3-stack) | 패널 ≤ 600px |
| 패널 + 결론 (2-stack) | 패널 ≤ 680px |

**계산 공식:**

```
패널 height = 780 - (타이틀 height + gap) - (결론 height + gap)
            = 780 - 80 - 100
            = 600 (3-stack 기준)
```

### 체크리스트 (수직 공간)

- [ ] 🔴 패널/박스에 `minHeight` 대신 `height` 사용했는가?
- [ ] 🔴 모든 콘텐츠 합 height ≤ 780px 인가?
- [ ] 🔴 단일 flex column으로 묶어 overflow 방지했는가?
- [ ] 결론 텍스트가 패널과 겹치지 않는가?
- [ ] 타이틀이 패널과 겹치지 않는가?

---

## 사례: SegmentG (트레이드오프 패널 적용)

**Before (별로):**
- 삼각형 배치 + 양방향 화살표 + floating 라벨
- 타이틀이 노드와 겹침
- 패널 minHeight 720 → 영역 overflow → 결론 텍스트가 패널과 겹침

**After (개선):**
- 좌우 패널 + 각각 ↓ 화살표 + ⚠ 워닝 칩 체인
- height: 640 fixed (안전 영역 내)
- 단일 flex column 컨테이너로 타이틀/패널/결론 자동 spacing
- 시각 차별화: 큰 단일 블록 vs 9개 작은 블록 (의미적 대비)

## 사례: SegmentC (라벨 블록 타워)

**Before (별로):**
- 라벨 없는 가로 막대 18개 stack
- "코드처럼 보이는" 더미 바 차트 — UI 로딩 스켈레톤 같음

**After (개선):**
- 9개 기능 라벨 블록 ("DB 스키마", "백엔드 API" 등)
- 녹색 솔리드 + 흰색 보더 교대 (시각 위계)
- 의미가 즉시 전달됨: "AI가 한 번에 9가지 기능을 다 만들었다"
