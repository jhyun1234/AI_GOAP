# 미니멀 디자인 언어 — 3D 모션 & 인포그래픽

미니멀 인포그래픽 스타일을 영상 인포그래픽에 적용. **색상은 3색 팔레트 유지** (#000000 + #FFFFFF + #00FF88). 변경되는 것은 **레이아웃 구조**, **깊이감 처리**, **장식 요소**, **모션 톤**.

---

## 미니멀 디자인 특징 — 영상에 적용할 핵심

미니멀 인포그래픽의 본질:

1. **카드 기반 메트릭 그리드** — 핵심 수치를 2-up / 3-up paired 카드로 묶음
2. **큰 숫자 + 작은 라벨** — 숫자가 주인공, 라벨은 1줄로 짧게
3. **3D 일러스트 hero** — 평평한 SVG가 아닌 깊이감 있는 아이콘 (그라데이션 + drop shadow)
4. **Sparkle 장식** — hero 주변 4-point 별 3-5개로 임팩트 강화
5. **친근한 톤** — 매트한 솔리드보다 부드러운 그림자/하이라이트
6. **성취 framing** — 1위, 돌파, 누적 같은 milestone에 월계관/방패/깃발 메타포
7. **스프링 모션** — 카드/아이콘 등장이 살짝 통통 튀는 spring (overshoot 1.05-1.1)

---

## 🔴 palette 내 depth 허용 — 그라데이션/그림자 규칙 완화

미니멀 스타일 적용을 위해 **palette 내부 그라데이션 + drop shadow**를 허용한다. **3색 팔레트 자체는 절대 안 깬다.**

### ✅ 허용 (palette 내부)

```tsx
// 그린 단색 → 그린 + 어두운 그린 (같은 hue, 깊이감용)
<linearGradient id="greenDepth">
  <stop offset="0%" stopColor="#00FF88" />
  <stop offset="100%" stopColor="#00CC6A" />  // 같은 hue, 어둡게
</linearGradient>

// 흰색 하이라이트 (radial, 입체감용)
<radialGradient id="whiteHighlight">
  <stop offset="0%" stopColor="rgba(255,255,255,0.4)" />
  <stop offset="100%" stopColor="rgba(255,255,255,0)" />
</radialGradient>

// drop shadow (그린 glow)
filter: 'drop-shadow(0 12px 32px rgba(0,255,136,0.35))'

// drop shadow (흰 hint)
filter: 'drop-shadow(0 8px 24px rgba(255,255,255,0.12))'

// 카드 box shadow (palette 내부)
boxShadow: '0 16px 40px rgba(0,255,136,0.18)'
boxShadow: '0 8px 24px rgba(0,0,0,0.5)'  // 검정 그림자도 OK
```

### ❌ 여전히 금지

```tsx
// 다른 hue로 그라데이션 (보라/파랑/주황 등) — 절대 금지
background: 'linear-gradient(to right, #8B5CF6, #00FF88)'  // ❌
background: 'linear-gradient(to right, #FFD700, #00FF88)'  // ❌

// 무지개 / 다채색 그라데이션
background: 'linear-gradient(135deg, red, orange, yellow)'  // ❌
```

### 정리

| 종류 | 허용 | 금지 |
|------|------|------|
| linear gradient (single hue 안에서) | ✅ #00FF88 → #00CC6A | ❌ #00FF88 → #FFD700 |
| radial highlight (흰색 하이라이트) | ✅ rgba(255,255,255,0.3→0) | ❌ 보라색/주황색 highlight |
| drop shadow (그린/검정) | ✅ rgba(0,255,136,0.3) | ❌ rgba(255,0,0,0.3) 빨강 |
| box shadow | ✅ palette 내부 색만 | ❌ 다른 색 그림자 |

---

## 패턴 16: 미니멀 메트릭 카드 그리드 (Paired Metric Cards)

**언제 사용**: 핵심 수치 2-4개를 동시에 보여줄 때. 도넛 하나만 던지지 말고 paired 카드로.

### 구조

```tsx
// 2-up grid (큰 메트릭 2개)
<SafeContentArea>
  <div style={{
    display: "grid",
    gridTemplateColumns: "1fr 1fr",
    gap: 40,
    width: "100%",
    maxWidth: 1400,
  }}>
    <MetricCard
      icon={<CoinStack3D startFrame={20} />}
      value="10조"
      label="누적 송금액"
      startFrame={20}
      isAccent
    />
    <MetricCard
      icon={<HandTap3D startFrame={30} />}
      value="1조"
      label="월 송금액"
      startFrame={30}
    />
  </div>
</SafeContentArea>
```

### MetricCard 컴포넌트

```tsx
const MetricCard: React.FC<{
  icon: React.ReactNode;
  value: string;
  label: string;
  startFrame: number;
  isAccent?: boolean;
}> = ({ icon, value, label, startFrame, isAccent = false }) => {
  const frame = useCurrentFrame();

  // 스프링 등장 — 살짝 overshoot
  const scale = interpolate(
    frame,
    [startFrame, startFrame + 12, startFrame + 22],
    [0.85, 1.05, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [startFrame, startFrame + 14], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 카운터 숫자 fade (icon 등장 후)
  const valueOpacity = interpolate(
    frame, [startFrame + 18, startFrame + 30], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <div style={{
      opacity,
      transform: `scale(${scale})`,
      padding: "48px 36px 40px",
      borderRadius: 32,
      border: `4px solid ${isAccent ? "#00FF88" : "rgba(255,255,255,0.18)"}`,
      backgroundColor: isAccent ? "rgba(0,255,136,0.06)" : "rgba(255,255,255,0.03)",
      boxShadow: isAccent
        ? "0 16px 48px rgba(0,255,136,0.22)"
        : "0 8px 24px rgba(255,255,255,0.06)",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      gap: 16,
      minHeight: 360,
    }}>
      {/* 3D icon 영역 (고정 높이로 흔들림 방지) */}
      <div style={{ height: 180, display: "flex", alignItems: "center" }}>
        {icon}
      </div>

      {/* 큰 숫자 — 카운터 fade-in */}
      <p style={{
        color: isAccent ? "#00FF88" : "#FFFFFF",
        fontSize: 128,
        fontWeight: 900,
        lineHeight: 1,
        opacity: valueOpacity,
        textShadow: isAccent
          ? "0 4px 24px rgba(0,255,136,0.4)"
          : "none",
      }}>
        {value}
      </p>

      {/* 작은 라벨 */}
      <p style={{
        color: "rgba(255,255,255,0.7)",
        fontSize: 34,
        fontWeight: 600,
        opacity: valueOpacity,
      }}>
        {label}
      </p>
    </div>
  );
};
```

### 변형: 3-up / 4-up 그리드

```tsx
// 3-up (한 줄)
<div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 32 }}>
  <MetricCard ... />
  <MetricCard ... />
  <MetricCard ... />
</div>

// 4-up (2x2)
<div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 32, width: 1400 }}>
  <MetricCard ... />
  <MetricCard ... />
  <MetricCard ... />
  <MetricCard ... />
</div>
```

### 핵심 원칙

1. **숫자는 항상 fontSize 100+ 굵게 (font-weight 900)**
2. **라벨은 fontSize 34 이하, font-weight 600, rgba 0.7**
3. **카드 padding 충분히** (40px 이상) — 답답하지 않게
4. **카드 등장 stagger 8-12 프레임씩 시간차**
5. **하나는 accent, 나머지는 중립** — 모든 카드를 강조하면 강조 무력화
6. **3-up까지** — 4개 넘으면 2x2 그리드로

---

## 패턴 17: 3D 깊이감 아이콘 (Depth Icon)

**언제 사용**: hero icon, MetricCard 안 icon, achievement badge. 평평한 단색 SVG는 영상 카드 안에서 빈약함.

### 기본 패턴 — 그라데이션 + 하이라이트 + drop shadow

```tsx
const CoinStack3D: React.FC<{
  startFrame: number;
  size?: number;
}> = ({ startFrame, size = 160 }) => {
  const frame = useCurrentFrame();

  const scale = interpolate(
    frame, [startFrame, startFrame + 12, startFrame + 22],
    [0.7, 1.1, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [startFrame, startFrame + 12], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 160 160"
      style={{
        opacity,
        transform: `scale(${scale})`,
        filter: "drop-shadow(0 12px 32px rgba(0,255,136,0.35))",
      }}
    >
      <defs>
        {/* 그린 depth gradient */}
        <linearGradient id="coinFill" x1="0%" y1="0%" x2="0%" y2="100%">
          <stop offset="0%" stopColor="#00FF88" />
          <stop offset="100%" stopColor="#00B85F" />
        </linearGradient>
        {/* 흰 하이라이트 */}
        <radialGradient id="coinHighlight" cx="35%" cy="30%" r="40%">
          <stop offset="0%" stopColor="rgba(255,255,255,0.5)" />
          <stop offset="100%" stopColor="rgba(255,255,255,0)" />
        </radialGradient>
      </defs>

      {/* 동전 3개 stacked — 아래에서 위로 */}
      <ellipse cx={80} cy={120} rx={50} ry={14} fill="url(#coinFill)" />
      <ellipse cx={80} cy={95} rx={50} ry={14} fill="url(#coinFill)" />
      <ellipse cx={80} cy={70} rx={50} ry={14} fill="url(#coinFill)" />

      {/* 동전 측면 (3D depth) */}
      <rect x={30} y={70} width={100} height={50} fill="url(#coinFill)" opacity={0.6} />

      {/* 하이라이트 (위쪽 동전에만) */}
      <ellipse cx={80} cy={70} rx={50} ry={14} fill="url(#coinHighlight)" />

      {/* 화폐 기호 */}
      <text x={80} y={78} textAnchor="middle" fontSize={20} fontWeight={900} fill="#003318">
        ₩
      </text>
    </svg>
  );
};
```

### 깊이감 SVG 체크리스트

- [ ] linear gradient로 fill (같은 hue 안에서 명도 변화)
- [ ] radial gradient로 하이라이트 (rgba(255,255,255,0.3-0.5) → 0)
- [ ] drop shadow filter 적용 (palette 내 색만)
- [ ] 메인 도형 + 측면 도형으로 2.5D 효과 (선택)
- [ ] 입체 윤곽선 strokeWidth 최소 3px
- [ ] 등장 애니메이션: scale 0.7 → 1.1 → 1.0 (살짝 overshoot)

### Hero 아이콘 라이브러리 패턴

미니멀 참조 이미지에서 자주 나오는 메타포들 — 깊이감 SVG로 표현:

| 메타포 | 용도 | 핵심 도형 | 난이도 |
|--------|------|----------|------|
| **동전 스택** | 누적 금액, 모은 자산 | 타원 3개 stacked + 측면 사각형 + 명도 차이 | 중 |
| **결제 카드** | 송금, 결제, 클릭 | 카드 + ₩ + NFC ring 호 | 쉬움 |
| **방패 + 자물쇠** | 보안, 안전, 0건 | 방패 outline + 자물쇠 중앙 | 쉬움 |
| **깃발** | 순위, 진출, 마일스톤 | 깃대 + 깃발 천 (depth) | 쉬움 |
| **메달 배지** | 1위, 수상, 성취 | 원형 메달 + 리본 + 중앙 숫자 | 쉬움 |
| **트로피** | 1위, 우승 | 받침대 + 컵 형태 + 손잡이 | 중 |
| **별 burst** | 임팩트, 강조 | 큰 별 + 방사형 spike | 쉬움 |
| **상자/패키지** | 출시, 배포 | 박스 outline + 윗면 사선 | 중 |
| **그래프 막대 3D** | 성장, 증가 | 사각형 3-4개 + 정면 그라디언트 | 쉬움 |
| **체크 동그라미** | 완료, 성공 | 원 + 체크마크 + radial highlight | 쉬움 |

### 🔴 어려운/금지 메타포

| 메타포 | 이유 | 대안 |
|--------|------|-----|
| **손/손가락** | 사람 부위 — "생물 SVG 금지" 규칙 위반 | **결제 카드 + NFC ring** |
| **사람 실루엣** | 비례 안 맞음 | **체스 폰/킹** 또는 **단순 원 + 라벨** |
| **월계관** | 잎사귀를 곡선 따라 정렬 어려움 — 콩알처럼 떠 보임 | **메달 배지** (원형 + 리본) 또는 **트로피** |
| **얼굴/눈/입** | 표현 불가 | **이모지** 사용 |

---

### 🔴 동전 스택 — 명확한 분리 규칙

동전 3개를 stack할 때 **각 동전 사이 명확한 명도 차이 + outline 필수**. 안 그러면 통원기둥 1개로 보인다.

```tsx
// ✅ 좋음 — 명도 단계 + 어두운 outline
const coins = [
  { brightness: 0 },  // 맨 아래 — #007A3C (어두운 그린)
  { brightness: 1 },  // 중간 — #00B85F
  { brightness: 2 },  // 맨 위 — #00FF88 (밝은 그린, highlight 적용)
];

// 각 동전마다:
// - 측면: 더 어두운 색 (sides[brightness])
// - 윗면: 본 색 (fills[brightness])
// - stroke="#003318" strokeWidth={2} (검은 outline 필수)
// - 동전 사이 6px 간격 (붙어 있으면 통원기둥처럼 보임)

// ❌ 나쁨 — 측면 한 통으로 + outline 없음
<rect x={30} y={70} width={100} height={50} fill="url(#coinFill)" opacity={0.6} />
// → 동전 3개가 통원기둥 1개처럼 보임
```

체크:
- [ ] 동전 3개가 각각 분리된 group (`<g>`)으로 그려졌는가?
- [ ] 각 동전 사이 최소 6px 간격 있는가?
- [ ] 위/중간/아래 동전 명도가 명확히 다른가?
- [ ] 검은 outline (stroke="#003318" strokeWidth={2~2.5}) 적용됐는가?
- [ ] 맨 위 동전에만 radial highlight 적용됐는가?

---

## 패턴 18: Sparkle / 4-Point Star 장식

**언제 사용**: hero icon 주변, achievement moment, 임팩트 강조. 빈 공간을 메우면서 시각적 활기.

### Sparkle SVG

```tsx
const Sparkle: React.FC<{
  startFrame: number;
  size?: number;
  color?: string;
  delay?: number;
}> = ({ startFrame, size = 32, color = "#FFFFFF", delay = 0 }) => {
  const frame = useCurrentFrame();
  const trigger = startFrame + delay;

  // 등장 — scale up + opacity
  const scale = interpolate(
    frame, [trigger, trigger + 10, trigger + 18],
    [0, 1.2, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const opacity = interpolate(
    frame, [trigger, trigger + 8], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 32 32"
      style={{
        opacity,
        transform: `scale(${scale})`,
        filter: `drop-shadow(0 0 8px ${color})`,
      }}
    >
      {/* 4-point star (위아래 + 좌우 뾰족) */}
      <path
        d="M 16 0 L 19 13 L 32 16 L 19 19 L 16 32 L 13 19 L 0 16 L 13 13 Z"
        fill={color}
      />
    </svg>
  );
};
```

### Sparkle Cluster — hero 주변 배치

```tsx
const SparkleCluster: React.FC<{ startFrame: number }> = ({ startFrame }) => (
  <div style={{ position: "absolute", inset: 0, pointerEvents: "none" }}>
    {/* 큰 sparkle */}
    <div style={{ position: "absolute", top: "10%", left: "15%" }}>
      <Sparkle startFrame={startFrame} size={40} color="#00FF88" delay={0} />
    </div>
    {/* 중간 sparkle */}
    <div style={{ position: "absolute", top: "20%", right: "20%" }}>
      <Sparkle startFrame={startFrame} size={28} color="#FFFFFF" delay={6} />
    </div>
    {/* 작은 sparkle */}
    <div style={{ position: "absolute", bottom: "25%", left: "25%" }}>
      <Sparkle startFrame={startFrame} size={20} color="#FFFFFF" delay={12} />
    </div>
    {/* 작은 sparkle */}
    <div style={{ position: "absolute", bottom: "15%", right: "15%" }}>
      <Sparkle startFrame={startFrame} size={24} color="#00FF88" delay={18} />
    </div>
  </div>
);
```

### Sparkle 사용 규칙

- **3-5개만** — 너무 많으면 산만함
- **크기 다양화** — 큰 거 1개 + 중간 1-2개 + 작은 2개
- **stagger 등장** — 5-7 프레임씩 시간차
- **루프 금지** — 등장 한 번만, 펄스 반복 금지 (어지러움)
- **hero 주변 30-50% 거리** — 너무 가까우면 hero 가림

---

## 패턴 19: 성취 배지 (Achievement Badge)

**언제 사용**: "1위", "100%", "0건 사고", "돌파", milestone moment.

### 🔴 월계관 메타포 금지 — 메달 배지 사용

월계관 잎사귀를 곡선 따라 정렬하는 게 어려워서 잎사귀가 곡선과 분리되어 콩알처럼 떠 보인다. **원형 메달 배지** 또는 **별 burst** 메타포를 쓴다.

### 메달 배지 — 원형 메달 + 리본 + 중앙 숫자

```tsx
const AchievementBadge: React.FC<{
  startFrame: number;
  label: string;        // "1위", "100%", "0건"
  description?: string; // "간편송금 압도적 1위"
}> = ({ startFrame, label, description }) => {
  const frame = useCurrentFrame();

  // 메달 등장 — scale + rotation 살짝
  const medalScale = interpolate(
    frame, [startFrame, startFrame + 14, startFrame + 26],
    [0.5, 1.1, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const medalOpacity = interpolate(
    frame, [startFrame, startFrame + 14], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 리본 등장 — 메달 뒤에서 펼쳐짐
  const ribbonProgress = interpolate(
    frame, [startFrame + 10, startFrame + 30], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 중앙 라벨
  const labelOpacity = interpolate(
    frame, [startFrame + 22, startFrame + 36], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  const labelScale = interpolate(
    frame, [startFrame + 22, startFrame + 32, startFrame + 42],
    [0.6, 1.15, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );

  // 방사형 spike (배경)
  const spikes = 12;

  return (
    <div style={{ position: "relative", display: "flex", flexDirection: "column", alignItems: "center" }}>
      <div style={{ position: "relative", width: 460, height: 520 }}>
        <svg
          width={460} height={520} viewBox="0 0 460 520"
          style={{
            opacity: medalOpacity,
            transform: `scale(${medalScale})`,
            filter: "drop-shadow(0 16px 40px rgba(0,255,136,0.4))",
          }}
        >
          <defs>
            <radialGradient id="medalFill" cx="35%" cy="30%" r="65%">
              <stop offset="0%" stopColor="#00FF88" />
              <stop offset="100%" stopColor="#00B85F" />
            </radialGradient>
            <radialGradient id="medalHL" cx="35%" cy="25%" r="40%">
              <stop offset="0%" stopColor="rgba(255,255,255,0.5)" />
              <stop offset="100%" stopColor="rgba(255,255,255,0)" />
            </radialGradient>
            <linearGradient id="ribbonFill" x1="0%" y1="0%" x2="0%" y2="100%">
              <stop offset="0%" stopColor="#FFFFFF" />
              <stop offset="100%" stopColor="rgba(255,255,255,0.7)" />
            </linearGradient>
          </defs>

          {/* 리본 왼쪽 (뒤에서 펼쳐짐) */}
          <path
            d={`M 180 240 L 100 ${240 + ribbonProgress * 240} L 140 ${
              240 + ribbonProgress * 200
            } L 200 ${260 + ribbonProgress * 60} Z`}
            fill="url(#ribbonFill)"
            stroke="#003318"
            strokeWidth={2}
            opacity={ribbonProgress}
          />
          {/* 리본 오른쪽 */}
          <path
            d={`M 280 240 L 360 ${240 + ribbonProgress * 240} L 320 ${
              240 + ribbonProgress * 200
            } L 260 ${260 + ribbonProgress * 60} Z`}
            fill="url(#ribbonFill)"
            stroke="#003318"
            strokeWidth={2}
            opacity={ribbonProgress}
          />

          {/* 방사형 spike (메달 뒤 배경) */}
          {Array.from({ length: spikes }).map((_, i) => {
            const angle = (i / spikes) * 360;
            const rad = (angle * Math.PI) / 180;
            const cx = 230;
            const cy = 230;
            const innerR = 160;
            const outerR = 195;
            const x1 = cx + Math.cos(rad - 0.08) * innerR;
            const y1 = cy + Math.sin(rad - 0.08) * innerR;
            const x2 = cx + Math.cos(rad) * outerR;
            const y2 = cy + Math.sin(rad) * outerR;
            const x3 = cx + Math.cos(rad + 0.08) * innerR;
            const y3 = cy + Math.sin(rad + 0.08) * innerR;
            return (
              <path
                key={i}
                d={`M ${x1} ${y1} L ${x2} ${y2} L ${x3} ${y3} Z`}
                fill="rgba(0,255,136,0.3)"
              />
            );
          })}

          {/* 메달 본체 (원) */}
          <circle
            cx={230} cy={230} r={150}
            fill="url(#medalFill)"
            stroke="#003318" strokeWidth={3}
          />
          {/* 메달 하이라이트 */}
          <circle cx={230} cy={230} r={150} fill="url(#medalHL)" />
          {/* 메달 내부 링 */}
          <circle
            cx={230} cy={230} r={125}
            fill="none"
            stroke="rgba(255,255,255,0.3)"
            strokeWidth={4}
          />
        </svg>

        {/* 중앙 라벨 */}
        <div style={{
          position: "absolute",
          top: 0, left: 0, width: 460, height: 460,
          display: "flex", alignItems: "center", justifyContent: "center",
          opacity: labelOpacity,
          transform: `scale(${labelScale})`,
        }}>
          <p style={{
            fontSize: 160, fontWeight: 900,
            color: "#FFFFFF",
            textShadow: "0 4px 24px rgba(0,0,0,0.4)",
            lineHeight: 1,
            margin: 0,
          }}>
            {label}
          </p>
        </div>
      </div>

      {description && (
        <p style={{
          color: "rgba(255,255,255,0.7)", fontSize: 36, fontWeight: 600,
          marginTop: 16, opacity: labelOpacity,
          textAlign: "center",
        }}>
          {description}
        </p>
      )}
    </div>
  );
};
```

### 핵심 규칙

- **원형 메달 본체** — 단일 큰 원, radial gradient + highlight
- **방사형 spike 배경** — 12개 spike로 임팩트 강화 (loop 금지, 정적)
- **리본 좌우 펼침** — 메달 뒤에서 아래로 펼쳐지는 애니메이션
- **중앙 텍스트는 흰색** — 메달 그린 위 흰색이 가장 잘 보임
- **검정 outline 필수** — stroke="#003318" strokeWidth={3} (메달이 배경과 분리)

### 변형: 방패 + 자물쇠 (보안/안전 milestone)

"0건 사고", "100% 안전" 같은 보안 metric:

```tsx
// 방패 outline + 자물쇠 중앙 + radial highlight
<svg width={300} height={340} viewBox="0 0 300 340">
  <defs>
    <linearGradient id="shieldFill" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stopColor="#00FF88" />
      <stop offset="100%" stopColor="#00B85F" />
    </linearGradient>
  </defs>
  {/* 방패 형태 */}
  <path
    d="M 150 20 L 280 70 L 280 170 Q 280 280 150 320 Q 20 280 20 170 L 20 70 Z"
    fill="url(#shieldFill)"
    filter="drop-shadow(0 12px 24px rgba(0,255,136,0.4))"
  />
  {/* 자물쇠 몸체 */}
  <rect x={110} y={150} width={80} height={70} rx={8} fill="#FFFFFF" />
  {/* 자물쇠 고리 */}
  <path
    d="M 125 150 L 125 120 Q 125 100 150 100 Q 175 100 175 120 L 175 150"
    fill="none" stroke="#FFFFFF" strokeWidth={10} strokeLinecap="round"
  />
</svg>
```

---

## 패턴 21: 가로 카드 데이터 그리드 (Horizontal Metric Grid)

**언제 사용**: 핵심 지표 6-10개를 한 화면에 동시에 보여주는 대시보드 화면. 2-up paired보다 더 많은 metric을 한 번에.

### 구조

- **2열 × 3-5행 그리드** (총 6-10개 카드)
- 각 카드: **가로 layout** (좌측 라벨+숫자, 우측 작은 아이콘 배지)
- 카드 padding 적절히, 카드 사이 gap 20px
- 1-2개만 accent (그린 보더 + 그린 숫자), 나머지는 중립

### HorizontalMetricCard 컴포넌트

```tsx
const HorizontalMetricCard: React.FC<{
  label: string;
  value: string;
  icon: React.ReactNode;
  startFrame: number;
  isAccent?: boolean;
}> = ({ label, value, icon, startFrame, isAccent = false }) => {
  const frame = useCurrentFrame();
  const scale = interpolate(
    frame, [startFrame, startFrame + 12, startFrame + 22],
    [0.9, 1.04, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" }
  );
  const opacity = interpolate(frame, [startFrame, startFrame + 14], [0, 1],
    { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  return (
    <div style={{
      opacity, transform: `scale(${scale})`,
      padding: "28px 32px",
      borderRadius: 24,
      border: `3px solid ${isAccent ? "#00FF88" : "rgba(255,255,255,0.14)"}`,
      backgroundColor: isAccent
        ? "rgba(0,255,136,0.07)"
        : "rgba(255,255,255,0.035)",
      boxShadow: isAccent
        ? "0 12px 32px rgba(0,255,136,0.18)"
        : "0 6px 20px rgba(0,0,0,0.4)",
      display: "flex",
      alignItems: "center",
      justifyContent: "space-between",
      gap: 24,
      minHeight: 140,
    }}>
      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
        <p style={{
          color: "rgba(255,255,255,0.7)",
          fontSize: 22, fontWeight: 600, margin: 0,
        }}>{label}</p>
        <p style={{
          color: isAccent ? "#00FF88" : "#FFFFFF",
          fontSize: 60, fontWeight: 900, lineHeight: 1, margin: 0,
        }}>{value}</p>
      </div>
      <div style={{ flexShrink: 0 }}>{icon}</div>
    </div>
  );
};
```

### 그리드 배치

```tsx
<div style={{
  display: "grid",
  gridTemplateColumns: "1fr 1fr",
  gap: 20,
  width: "100%",
  maxWidth: 1500,
}}>
  {items.map((item, i) => (
    <HorizontalMetricCard
      key={item.label}
      {...item}
      startFrame={20 + i * 8}  // 8 프레임 stagger
    />
  ))}
</div>
```

### 작은 아이콘 배지 라이브러리 (80-100px)

각 카드 우측에 들어가는 작은 SVG 배지. 같은 gradient + drop shadow 스타일 통일:

| 배지 | 메타포 | 용도 |
|------|--------|------|
| CoinsBadge | 동전 cluster | 송금액, 자산 |
| BoltBadge | 번개 in circle | 속도, 즉시, 활성 |
| CardBadge | 단일 카드 | 등록, 인증 |
| CardStackBadge | 카드 stack | 다중 카드, 결제수단 |
| ChartBadge | 트렌드 라인 | 성장, 투자, 추세 |
| GaugeBadge | 반원 게이지 | 점수, 등급, 진행률 |
| LayersBadge | 3-stack sheet | 다중 서비스, 카테고리 |
| GroupBadge | 체스 폰 3개 | 사용자, 가입자, 인원 (사람 메타포) |

### 핵심 원칙

- **카드 사이 gap 20px** — 너무 좁으면 답답, 너무 넓으면 흩어짐
- **카드 minHeight 140** — 라벨 단어 길이 달라도 정렬 유지
- **숫자 fontSize 60** — 카드 안에서 크고 굵게, hero metric보다는 작게
- **라벨 fontSize 22** — 가독성 유지하면서 layout 깔끔
- **stagger 8 프레임씩** — 8개 카드 64 프레임 ≈ 1초 안에 다 등장
- **1-2개만 accent** — 전부 강조하면 강조 의미 없음
- **카드 corner radius 24px** — 부드러운 느낌

### 활용 예

- 서비스 KPI 대시보드 (가입자 / 송금액 / 거래량 등)
- 분기 결과 요약 (매출 / 사용자 / 수익률 등)
- 채널 통계 (구독자 / 조회수 / 평균 시청시간 등)

---

## 패턴 20: 미니멀 스타일 인트로 — 큰 타이틀 + paired metric tease

미니멀의 첫 화면은 "예시 월 송금액 1조, 누적 송금액 10조 돌파" 같은 **큰 타이틀 + 핵심 수치 inline tease**다. 영상 인트로에 적용.

### 구조

```tsx
<SafeContentArea>
  <div style={{
    display: "flex", flexDirection: "column", alignItems: "center", gap: 60,
  }}>
    {/* 상단: 큰 타이틀 (영문 브랜드 OK인 경우만, 한글 우선) */}
    <p style={{
      fontSize: 96, fontWeight: 900,
      color: "#FFFFFF",
      lineHeight: 1.15,
      textAlign: "center",
    }}>
      월 송금액 <span style={{ color: "#00FF88" }}>1조</span><br/>
      누적 송금액 <span style={{ color: "#00FF88" }}>10조</span> 돌파
    </p>

    {/* 우상단 sparkle decoration */}
    <SparkleCluster startFrame={startFrame} />

    {/* hero icon (동전 + 손) */}
    <div style={{ display: "flex", gap: 48, alignItems: "center" }}>
      <CoinStack3D startFrame={startFrame + 10} size={200} />
      <HandTap3D startFrame={startFrame + 20} size={200} />
    </div>
  </div>
</SafeContentArea>
```

### 인트로 원칙

- **타이틀에 핵심 수치 inline 강조** (그린으로) — 본문 카드와 시각 연결
- **hero icon 2개 paired** — 단일 icon보다 두 개가 미니멀 톤
- **sparkle은 타이틀 주변 분산** — icon 가리지 않게
- **타이틀 등장 → sparkle → icon 순서 stagger**

---

## 종합 적용 예시: 1분 미니멀 스타일 인포그래픽 영상 구조

```
Segment 1 (0-5s): 인트로 — 패턴 20 (큰 타이틀 + paired hero)
Segment 2 (5-15s): 카드 그리드 1 — 패턴 16 (2-up: 10조 / 1조)
Segment 3 (15-25s): 카드 그리드 2 — 패턴 16 (2-up: 1200만 / 650만)
Segment 4 (25-35s): 성취 — 패턴 19 (35위 깃발 + 지구본 메타포)
Segment 5 (35-45s): 성취 — 패턴 19 (1위 + 월계관)
Segment 6 (45-55s): 보안 — 패턴 19 변형 (방패 + 자물쇠 + 0건)
Segment 7 (55-60s): 마무리 — 패턴 16 단일 큰 카드
```

각 세그먼트마다 **3-5 sparkle 장식** + **drop shadow depth** + **stagger 카드 등장**.

---

## 미니멀 적용 체크리스트

- [ ] 메트릭이 2개 이상이면 paired card grid 사용 (단일 도넛 X)
- [ ] 모든 hero icon에 gradient + drop shadow 적용 (평평 SVG X)
- [ ] hero 주변에 sparkle 3-5개 배치 (루프 금지)
- [ ] 카드 등장에 spring overshoot (scale 0.85 → 1.05 → 1)
- [ ] 숫자는 fontSize 100+ font-weight 900, 그린 강조
- [ ] 라벨은 fontSize 34 이하, rgba(255,255,255,0.7), 1줄
- [ ] 성취 moment(1위, 돌파 등)는 **메달 배지/깃발/방패** 메타포 (월계관 X)
- [ ] palette 내부 gradient만 사용 (다른 hue 금지)
- [ ] drop shadow는 palette 색만 (rgba(0,255,136), rgba(255,255,255), rgba(0,0,0))
- [ ] 카드 그리드는 max-w 1400px 안에 fit
- [ ] minHeight 금지, fixed height 사용 (LAYOUT_AND_PATTERNS.md 참조)
- [ ] 🔴 **사람 SVG 금지 — 손, 손가락, 얼굴 직접 그리지 마라** (결제 카드 + NFC ring으로 대체)
- [ ] 🔴 **월계관 SVG 금지 — 잎사귀 곡선 정렬 어려움** (메달 배지로 대체)
- [ ] 🔴 **동전 스택: 각 동전 사이 명도 차이 + 검은 outline 필수** (안 그러면 통원기둥처럼 보임)
- [ ] 🔴 **모든 3D SVG에 검은 outline (stroke="#003318" strokeWidth={2~3})** (배경과 분리)

---

## 미니멀 안 쓰는 곳 — 기존 패턴 유지

미니멀 스타일은 **데이터 메트릭 + 성취/milestone**에 적합. 아래는 기존 SKILL.md 패턴 그대로:

- 프로세스/단계 → ProcessStep (패턴 4)
- 비교 막대 → ComparisonBar (패턴 2)
- 도넛 차트 단일 → DonutSegment (패턴 3)
- 노드 관계도 → NodeMap (패턴 8)
- 다이어그램 → Diagram Mermaid/D2 (패턴 11)
- 트레이드오프 → TradeoffPanel (패턴 15)

미니멀 스타일은 위 패턴들과 **혼용 가능**. 예: 도넛 차트를 카드 안에 넣고 sparkle로 장식.
