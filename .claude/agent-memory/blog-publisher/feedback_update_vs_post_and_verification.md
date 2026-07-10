---
name: feedback-update-vs-post-and-verification
description: DRAFT 글을 새로 게시하지 말고 update 명령으로 교체할 것 + 게시 후 무결성 검증 시 스트림 청크를 문자열로 이어붙이면 안 됨
metadata:
  type: feedback
---

기존 DRAFT 글의 본문을 v2로 바꿀 때는 `blogger-client.js post`로 새 글을 만들지 않고
`blogger-client.js update --post-id <id> ...`로 같은 post-id를 교체한다. `update`는 발행
상태(DRAFT/LIVE)를 그대로 유지한 채 title/content/labels만 PUT으로 덮어쓴다.

**Why:** 마스터가 "v2로 교체"를 지시했는데 신규 게시를 하면 같은 소재로 중복 post가
생기고, `blog_last_published_commit.md`의 `blogger_post_id`/`blog_url` 이력도 깨진다.

**How to apply:** 오케스트레이션 프롬프트나 마스터 지시에 "기존 글 교체/DRAFT 교체"라는
표현이 나오면 반드시 `update` 명령 + 기존 post-id를 사용한다. 새 소재를 완전히 새로
게시하는 경우에만 `post` 명령을 쓴다.

---

게시(또는 교체) 후 실제로 서버에 저장된 콘텐츠가 로컬 원본과 바이트 단위로 동일한지
검증할 때, HTTPS 응답 스트림을 `res.on('data', c => data += c)` 식으로 **문자열
연결(string concatenation)** 하면 안 된다. 멀티바이트 UTF-8 문자(한글 등)가 TCP data
이벤트 청크 경계에서 잘리면 각 청크를 개별적으로 `toString()`할 때 `U+FFFD`(깨진 문자)로
보이고, 실제로는 손상되지 않은 데이터가 손상된 것처럼 오판하게 된다.

**Why:** 이번 v2 교체 검증 중 이 패턴 때문에 "목표 슬롯" 같은 단어가 깨져 보여 데이터
손상으로 오인했다. `Buffer.concat(chunks)` 후 한 번에 `toString('utf8')`로 바꾸니 완전히
동일했다 — 실제로는 손상이 전혀 없었다.

**How to apply:** 게시 결과의 콘텐츠 무결성을 재확인할 필요가 있을 때는 반드시
`const chunks = []; res.on('data', c => chunks.push(c)); res.on('end', () => Buffer.concat(chunks).toString('utf8'))`
패턴을 쓴다. `blogger-client.js`의 `httpsJson` 내부도 같은 문자열 연결 패턴을 쓰고
있으므로, 콘솔에 한글이 깨져 보여도 그 자체만으로 실제 게시 실패나 데이터 손상으로
단정하지 말고 별도 GET + Buffer 비교로 재검증할 것. (단, `httpsJson`을 고쳐야 하는지는
이 세션에서는 판단하지 않았다 — 실사용에는 영향 없으므로.)
