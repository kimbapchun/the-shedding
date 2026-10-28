using System.Collections.Generic;
using System.Linq;
using TheShedding.Characters;

namespace TheShedding.GameManagement
{
    /// <summary>서버 자동 배정용 명단. 기존 플레이어 역할은 늦은 접속으로 바꾸지 않는다.</summary>
    public sealed class RoleRoster
    {
        private readonly Dictionary<string, Role> assignments = new();
        public Role Assign(string playerId, int playerCount, int playersPerRobber, IEnumerable<Role> available)
        {
            if (assignments.TryGetValue(playerId, out var current)) return current;
            var choices = new HashSet<Role>(available);
            int required = System.Math.Max(1, (playerCount + System.Math.Max(1, playersPerRobber) - 1)
                / System.Math.Max(1, playersPerRobber));
            Role role = Role.Unassigned;
            if (choices.Contains(Role.Robber) && assignments.Values.Count(r => r == Role.Robber) < required)
                role = Role.Robber;
            else
                foreach (var family in new[] { Role.Mother, Role.Baby, Role.Father, Role.Dog })
                    if (choices.Contains(family) && !assignments.ContainsValue(family))
                    {
                        role = family;
                        break;
                    }
            if (role != Role.Unassigned) assignments.Add(playerId, role);
            return role;
        }
        public void Release(string playerId) => assignments.Remove(playerId);
        public void Clear() => assignments.Clear();
    }

    public static class CharacterRole
    {
        public static Role Resolve(BaseCharacterController character) => character switch
        {
            MotherController => Role.Mother,
            BabyController => Role.Baby,
            FatherController => Role.Father,
            DogController => Role.Dog,
            RobberController => Role.Robber,
            _ => Role.Unassigned
        };
    }
}
