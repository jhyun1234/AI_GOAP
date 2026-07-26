# AI_GOAP

Unity 탑다운 마을 생존 시뮬레이션. GOAP(Burst Job A\*) 기반 주민 AI가 핵심입니다.

주민은 목표를 세우고 계획을 짜서 스스로 움직이며, **플레이어의 명령을 거부할 수 있습니다.**
"자기 생각이 있는 AI와 협상하는 게임"이 이 프로젝트의 정체성입니다.

- Unity **6000.4.7f1**
- 씬: `Assets/Scenes/M0Scene.unity` (유일한 씬)
- 작업 규칙·아키텍처 기준: [`Docs/CLAUDE.md`](Docs/CLAUDE.md)
- 설계 사고법: [`Docs/개발_방법론_명세서.md`](Docs/개발_방법론_명세서.md)
- 개발 일지: [`devlog/INDEX.md`](devlog/INDEX.md)

## 에셋 조달 (클론 후 필수)

이 저장소에는 **서드파티 아트 에셋이 포함되어 있지 않습니다.** 재배포 라이선스가 불명확해
`.gitignore`로 제외했습니다. 클론 직후에는 스프라이트 참조가 비어 있으므로 아래를 직접 넣어야 합니다.

**Cute Fantasy RPG — 16x16 top down pixel art asset pack** (Kenmi)

1. 원 배포처에서 팩을 받습니다.
2. 압축을 풀어 아래 경로에 놓습니다:

```
Assets/Kenmi/Cute Fantasy RPG - 16x16 top down pixel art asset pack/
```

3. Unity를 열어 임포트가 끝나면 `.meta` 파일이 생성되면서 참조가 다시 연결됩니다.

이 팩이 없으면 다음 세 에셋의 스프라이트 참조가 깨집니다:

| 에셋 | 참조 대상 |
|---|---|
| `Assets/M0Config/VillagerSprites.asset` | `Sprites/Player/Player.png` |
| `Assets/M0Config/Buildings/House.asset` | `Sprites/House/Buildings/Tent/Tent_Small.png` |
| `Assets/M0Config/CropSprites.asset` | `Sprites/Crops/Crops.png` |

시뮬레이션 로직 자체는 아트 없이도 동작하지만, 주민·집·작물이 화면에 보이지 않습니다.

## 빌드·테스트

```bash
dotnet build AIVillage.csproj
```

테스트는 Unity Editor의 **Test Runner → EditMode**에서 실행합니다. 게이트 전체 green이
커밋 조건입니다 (자세한 절차는 `Docs/CLAUDE.md` "커밋 전 체크").
