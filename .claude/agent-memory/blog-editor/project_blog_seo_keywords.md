---
name: project-blog-seo-keywords
description: AI_GOAP 개발 블로그 회차별로 사용된 seo_keywords와 Blogger 라벨 이력
metadata:
  type: project
---

회차별 기획팀 seo_keywords / 편집팀이 정한 라벨 기록.

- 2026-07-09 (경로탐색 좌표 스냅 버그 수정 편): seo_keywords = ["유니티 GOAP AI 개발일지", "인디게임 경로탐색 버그 수정"]. 라벨 = ["유니티 GOAP AI 개발일지", "인디게임 경로탐색 버그 수정", "GOAP", "인디게임 개발"].
- 2026-08-08 (M15 연대기 아카이브 편, `05_final.md`): seo_keywords(기획팀) = ["Claude Code 게임 개발", "AI 페어 프로그래밍 후기"]. 라벨 = ["Unity GOAP 연대기 아카이브", "Claude Code 게임 개발", "AI 페어 프로그래밍 후기", "인디게임 개발일지"]. "Claude Code 게임 개발"·"AI 페어 프로그래밍 후기"는 최근 회차들에서 반복 채택되는 고정 seo_keywords 쌍으로 보임(과거 스테이징 잔재 M10 편 라벨에서도 동일 쌍 확인).

**Why:** 같은 니치 키워드("유니티 GOAP AI 개발일지", "Claude Code 게임 개발", "AI 페어 프로그래밍 후기" 등)가 여러 회차에서 반복 사용될 가능성이 높으므로, 라벨을 회차마다 임의로 새로 만들지 않고 기존 라벨 체계를 재사용하면 Blogger 라벨 페이지가 일관되게 쌓여 SEO에 유리하다.

**How to apply:** 새 초안을 받으면 이 목록에서 겹치는 seo_keywords가 있는지 먼저 확인하고, 기존 라벨을 우선 재사용한다. 새로운 회차 완료 후 이 파일에 항목을 추가한다.

**제목 배치 관례 (2026-08-08 확인):** 실제 산출물(M10 위협 편 스테이징 잔재, M14 발행 HTML 미확인이나 M10 잔재로 관찰)에서 제목 구조는 "Unity GOAP <소재> 인디게임 개발일지: <구체적 훅> (<seo_keywords 괄호>)" 순서 — 니치 키워드를 제목 맨 앞에, 훅을 콜론 뒤에, 기획팀 seo_keywords는 끝 괄호에 배치한다. blog-editor.md 4단계 "제목 앞부분에 자연스럽게" 지침과 일치. 다음 회차도 이 순서를 기본값으로 쓴다.
