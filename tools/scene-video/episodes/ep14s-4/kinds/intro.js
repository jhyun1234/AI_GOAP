/* 시리즈 공용 인트로 재수출 (ADR-V25-11) — 직접 그리지 않는다.
   브랜딩은 매회 같아야 브랜딩이라 정본이 engine/intro-brand.js 하나다.
   회차 폴더에 파일이 있어야 하는 이유는 check.mjs 의 `kind 파일 존재` 가
   episodes/<ep>/kinds/ 만 보기 때문이다.

   🔴 이 회차의 그림 축(가운데 불 하나와 그 둘레의 여섯 — 여섯이 어디에 서는가)을
   인트로에까지 밀어 넣지 않았다. 「그림은 회차가 소유한다」의 명시된 예외가 여기다. */
export { default } from '../../../engine/intro-brand.js';
