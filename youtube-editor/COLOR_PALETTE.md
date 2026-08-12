# 색상 팔레트 가이드

**3색 제한 팔레트. 이 3가지 색만 사용할 것. 예외 없음.**

## 팔레트

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│  ⚫ #000000 — 배경                                      │
│     기본 배경. 순수 검정. 회색/다크그레이 변형 금지.     │
│                                                         │
│  ⚪ #FFFFFF — 텍스트 & 선                               │
│     기본 텍스트, SVG stroke, 보조 도형.                  │
│     보조 라벨: rgba(255,255,255,0.7) 허용 (최소치).     │
│                                                         │
│  🟢 #00FF88 — 유일한 강조색 (네온 그린)                  │
│     모든 강조, 하이라이트, 긍정, 핵심, 데이터 시각화.   │
│     "이 색 하나로 모든 것을 강조한다."                   │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## 🔴 왜 3색인가?

- **일관성**: 색이 많으면 시청자가 "이 색은 뭘 의미하지?" 혼란
- **임팩트**: 검정 바다에서 네온 그린 하나만 빛나면 눈이 자동으로 간다
- **브랜드 정체성**: 3색은 채널의 시각적 시그니처가 된다
- **AI 슬롭 방지**: 색을 늘리면 AI가 무지개처럼 색을 뿌린다

## CSS 변수

```tsx
const COLORS = {
  bg: '#000000',
  text: '#FFFFFF',
  accent: '#00FF88',
} as const;
```

---

## 사용 패턴

### 배경
```tsx
// 항상 순수 검정
<AbsoluteFill style={{ backgroundColor: '#000000' }}>
```

### 기본 텍스트
```tsx
<p style={{ color: '#FFFFFF' }}>기본 텍스트</p>
```

### 보조 텍스트 (최소 0.7)
```tsx
<p style={{ color: 'rgba(255,255,255,0.7)' }}>보조 라벨</p>
```

### 강조 — 네온 그린 (유일한 강조색)
```tsx
// 핵심 키워드
<span style={{ color: '#00FF88' }}>핵심</span>

// 차트 데이터
<circle stroke="#00FF88" strokeWidth={5} />

// 카운터 숫자
<p style={{ color: '#00FF88' }}>87%</p>

// 긍정/성공/현재/강조/핵심/프리미엄/경고 — 전부 #00FF88
// 이전에 골드(#FFD700)나 레드(#FF3232)를 쓰던 자리도 전부 #00FF88
```

---

## 🔴 3색 일관성 규칙

**모든 세그먼트, 모든 프레임에서 3색만 보여야 한다.**

```
✅ 허용하는 색:
  #000000 — 배경
  #FFFFFF — 텍스트, SVG stroke
  rgba(255,255,255,0.7) — 보조 라벨 (0.7이 최소)
  rgba(255,255,255,0.1~0.15) — 빈 트랙, 가이드선 (배경 요소)
  #00FF88 — 유일한 강조색

❌ 절대 금지:
  #FFD700 — 골드 (삭제됨)
  #FF3232 — 레드 (삭제됨)
  모든 Tailwind 색상 클래스
  보라색, 파란색, 주황색, 분홍색, 아무 색이나
  그라데이션 (검정→검정 미세 변형 제외)
  rgba(255,255,255,0.4) 이하 투명도
```

### 비교/대비 표현 (레드 없이)

이전에 "나쁜 것 = 빨강, 좋은 것 = 초록"이었다면, 이제는:

```tsx
// ✅ 3색으로 비교 표현
// 나쁜 것 = 흰색 (중립)  |  좋은 것 = 네온 그린 (강조)
<ComparisonBar label="기존 방식" value={80} color="#FFFFFF" />      // 흰색 = 대조 대상
<ComparisonBar label="새로운 방식" value={20} color="#00FF88" />    // 그린 = 강조 대상

// ✅ 3색으로 경고 표현
// 경고도 #00FF88 — 맥락으로 부정/긍정 구분 (색이 아닌 아이콘/텍스트로)
<AlertIcon color="#00FF88" />
<p style={{ color: '#FFFFFF' }}>주의: 이 방식은 위험합니다</p>

// ❌ 금지: 빨강으로 부정 표현
<span style={{ color: '#FF3232' }}>실패</span>  // 금지
```

### 핵심 강조 (골드 없이)

```tsx
// ✅ 핵심 강조도 네온 그린
<p style={{ color: '#00FF88' }}>핵심 포인트</p>

// ✅ 프리미엄/중요도 차이는 크기와 굵기로
<p className="text-9xl font-black" style={{ color: '#00FF88' }}>₩0</p>  // 큰 = 중요
<p className="text-4xl" style={{ color: '#FFFFFF' }}>비용 절감</p>       // 작은 = 보조

// ❌ 금지: 골드로 강조
<span style={{ color: '#FFD700' }}>중요</span>  // 금지
```

---

## 색상 전략

### 한 프레임에 패턴 하나

검정 배경 위에 흰색 + 그린만 보인다. 깔끔하고 강렬하다.

```tsx
// ✅ 표준 패턴: 흰색 텍스트 + 그린 강조
<SafeContentArea>
  <p className="text-4xl" style={{ color: 'rgba(255,255,255,0.7)' }}>
    개발 비용
  </p>
  <p className="text-9xl font-black" style={{ color: '#00FF88' }}>
    ₩0
  </p>
</SafeContentArea>
```

### SVG 인포그래픽 색상

```tsx
// 메인 도형: 항상 #00FF88
<circle stroke="#00FF88" strokeWidth={5} fill="none" />
<rect fill="#00FF88" />

// 보조 도형/선: #FFFFFF
<line stroke="#FFFFFF" strokeWidth={3} />

// 빈 트랙: rgba(255,255,255,0.1)
<circle stroke="rgba(255,255,255,0.1)" strokeWidth={5} />
```

---

## 금지 색상

```tsx
// ❌ 삭제된 색
color: '#FFD700'    // 골드 → 삭제. #00FF88 또는 #FFFFFF 사용
color: '#FF3232'    // 레드 → 삭제. #FFFFFF로 대조 표현

// ❌ Tailwind 색상 클래스 금지
className="text-gray-600"
className="text-violet-600"
className="text-red-500"
className="text-yellow-400"

// ❌ 보라색 그라데이션 금지
background: 'linear-gradient(to right, #8B5CF6, #6366F1)'

// ❌ 다른 hue 간 그라데이션 금지 (palette 밖)
background: 'linear-gradient(to right, #00FF88, #FFD700)'  // 그린→골드 ❌

// ✅ palette 내 같은 hue 그라데이션은 허용 (미니멀 depth)
// 자세히 위 "미니멀 스타일 — palette 내 depth gradient" 섹션 참조
background: 'linear-gradient(180deg, #00FF88 0%, #00B85F 100%)'  // 같은 그린 ✅

// ❌ 오프화이트, 회색, 어두운 색 금지
color: '#E5E5E5'
color: '#9CA3AF'
backgroundColor: '#1F2937'
backgroundColor: '#333333'

// ❌ 반투명 텍스트 (0.7 미만)
color: 'rgba(255,255,255,0.4)'
color: 'rgba(255,255,255,0.5)'
style={{ opacity: 0.4 }}
```

---

## 미세 변형 (허용)

배경의 미세한 검정 변형은 허용:

```tsx
// ✅ 미세한 그라디언트 (검정 안에서)
background: 'linear-gradient(180deg, #000000 0%, #0a0a0a 100%)'

// ✅ 비네팅 효과
background: 'radial-gradient(ellipse at center, transparent 0%, #000000 100%)'
```

---

## 🟢 미니멀 스타일 — palette 내 depth gradient / drop shadow 허용

3D 모션/인포그래픽(미니멀 스타일) 적용 시, **3색 팔레트 내부 색만 사용한 그라데이션과 drop shadow를 허용한다.** 다른 hue(보라/파랑/주황 등)는 여전히 절대 금지.

### ✅ 허용 — palette 내부 depth 처리

```tsx
// 같은 hue 안에서 명도 그라데이션 (3D 입체감용)
<linearGradient id="greenDepth">
  <stop offset="0%" stopColor="#00FF88" />
  <stop offset="100%" stopColor="#00B85F" />  // 같은 그린, 더 어둡게
</linearGradient>

<linearGradient id="greenDepth2">
  <stop offset="0%" stopColor="#00FF88" />
  <stop offset="100%" stopColor="#00CC6A" />  // 같은 그린, 중간 명도
</linearGradient>

// 흰색 하이라이트 (radial, 입체감용)
<radialGradient id="whiteHighlight">
  <stop offset="0%" stopColor="rgba(255,255,255,0.4)" />
  <stop offset="100%" stopColor="rgba(255,255,255,0)" />
</radialGradient>

// drop shadow — 그린 glow (palette 내부)
style={{ filter: 'drop-shadow(0 12px 32px rgba(0,255,136,0.35))' }}

// drop shadow — 흰 hint
style={{ filter: 'drop-shadow(0 8px 24px rgba(255,255,255,0.12))' }}

// box shadow — palette 내부 색만
style={{ boxShadow: '0 16px 40px rgba(0,255,136,0.18)' }}
style={{ boxShadow: '0 8px 24px rgba(0,0,0,0.5)' }}  // 검정 그림자 OK

// 카드 미세 배경 (palette 내부)
style={{ backgroundColor: 'rgba(0,255,136,0.06)' }}     // 그린 미세
style={{ backgroundColor: 'rgba(255,255,255,0.03)' }}  // 흰 미세
```

### ❌ 여전히 금지 — palette 밖 색

```tsx
// 다른 hue gradient — 절대 금지
<stop stopColor="#8B5CF6" />  // ❌ 보라
<stop stopColor="#FFD700" />  // ❌ 골드
<stop stopColor="#3B82F6" />  // ❌ 파랑

// 다른 hue drop shadow — 절대 금지
filter: 'drop-shadow(0 12px 32px rgba(139,92,246,0.3))'  // ❌
filter: 'drop-shadow(0 8px 24px rgba(255,215,0,0.3))'    // ❌
```

### depth 사용 규칙

| 종류 | 허용 stop 색 | 허용 shadow rgba |
|------|------------|----------------|
| **그린 depth gradient** | `#00FF88` ~ `#00B85F` | `rgba(0,255,136, 0.1~0.4)` |
| **흰 하이라이트** | `rgba(255,255,255, 0.1~0.5)` ~ 0 | `rgba(255,255,255, 0.05~0.15)` |
| **검정 그림자** | `#000000` ~ `#1a1a1a` (사용 안 권장) | `rgba(0,0,0, 0.3~0.6)` |

### 핵심 원칙

1. **gradient는 단일 hue 안에서만** — 두 색이 다른 hue면 차단
2. **drop shadow는 palette 색만** — `rgba(0,255,136)`, `rgba(255,255,255)`, `rgba(0,0,0)`
3. **카드 배경은 미세하게** — `0.03~0.08` 정도. 너무 진하면 3색 규칙 깨짐
4. **메인 도형 fill은 여전히 솔리드 권장** — gradient는 큰 hero icon에만

자세한 미니멀 스타일 적용은 [MINIMAL_DESIGN_LANGUAGE.md](MINIMAL_DESIGN_LANGUAGE.md) 참조.

---

## 빠른 참조

| 용도 | 색상 | 코드 |
|------|------|------|
| 배경 | 검정 | `#000000` |
| 텍스트/선 | 흰색 | `#FFFFFF` |
| 보조 라벨 | 반투명 흰색 | `rgba(255,255,255,0.7)` |
| **모든 강조** | **네온 그린** | **`#00FF88`** |

**규칙**: 이 표에 없는 색은 사용하지 않는다. 3색. 끝.
