/* 시리즈 공용 인트로 재수출 (ADR-V25-11) — 직접 그리지 않는다.
   브랜딩은 매회 같아야 해서 정본이 engine/intro-brand.js 하나다.
   회차 폴더에 파일이 있어야 하는 이유는 check.mjs 의 `kind 파일 존재` 가
   episodes/<ep>/kinds/ 를 보기 때문이다. */
export { default } from '../../../engine/intro-brand.js';
