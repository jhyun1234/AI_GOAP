"""아주 작은 테스트 러너.

🔴 pytest 를 안 깐다. 이 저장소는 의존성 0 이 원칙이고(render.mjs 주석 참조), 검사 하나
   돌리자고 도구 체인을 늘리면 다음 사람의 기계에서 안 돈다. `python test_*.py` 로 끝난다.

쓰는 법: 파일 맨 아래에
    if __name__ == '__main__':
        from _check import run; run(globals())
"""
import sys
import traceback

# 윈도우 콘솔 기본 코드페이지가 cp949 라 한글·em-dash 가 터진다. 검사 도구가 인코딩으로
# 죽으면 실패 내용을 못 읽는다 — 그게 제일 나쁘다.
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass


def run(ns, name=None):
    tests = sorted((k, v) for k, v in ns.items()
                   if k.startswith('test_') and callable(v))
    if not tests:
        print('검사가 하나도 없다'); sys.exit(1)
    fails = []
    for k, fn in tests:
        try:
            fn()
            print('  ok    %s' % k)
        except Exception as e:                      # AssertionError 도 여기로 온다
            fails.append(k)
            print('  FAIL  %s — %s' % (k, e))
            if not isinstance(e, AssertionError):
                traceback.print_exc()
    title = name or ns.get('__file__', '?')
    print('%s: %d/%d 통과%s' % (title, len(tests) - len(fails), len(tests),
                              (' — 실패: ' + ', '.join(fails)) if fails else ''))
    sys.exit(1 if fails else 0)
