// AIVillage 어셈블리의 internal 접근자를 EditMode 테스트에서 참조하기 위한 설정.
// C2 T14: VillagerFSM의 P0 트리거 임계값(internal const)을 테스트가 상수 참조로 읽는다.
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AIVillage.Tests.EditMode")]
