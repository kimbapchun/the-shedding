using System.Collections.Generic;
using System.Linq;
using TheShedding.Characters;

namespace TheShedding.GameManagement
{
    /// <summary>서버 자동 배정용 명단. 기존 플레이어 역할은 늦은 접속으로 바꾸지 않는다.</summary>
    public sealed class RoleRoster
    {
        private readonly Dictionary<string, Role> assignments = new();
        /// <param name="selected">플레이어가 이미 고른 역할. 있으면 자동 배정보다 우선한다.</param>
        public Role Assign(string playerId, int playerCount, int playersPerRobber, ICollection<Role> choices,
            Role selected = Role.Unassigned)
        {
            if (assignments.TryGetValue(playerId, out var current)) return current;
            int required = System.Math.Max(1, (playerCount + System.Math.Max(1, playersPerRobber) - 1)
                / System.Math.Max(1, playersPerRobber));
            int robberCount = assignments.Values.Count(r => r == Role.Robber);
            if (selected != Role.Unassigned && choices.Contains(selected)
                && (selected == Role.Robber ? robberCount < required : !assignments.ContainsValue(selected)))
            {
                assignments.Add(playerId, selected);
                return selected;
            }
            Role role = Role.Unassigned;
            if (choices.Contains(Role.Robber) && robberCount < required)
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
