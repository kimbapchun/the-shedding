using System;
using TheShedding.Characters;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace TheShedding.Network
{
    /// <summary>체력과 상태이상의 종료 시각을 서버에서 복제한다. 늦은 접속도 현재 상태를 받는다.</summary>
    [RequireComponent(typeof(BaseCharacterController))]
    public class NetworkCharacterState : NetworkBehaviour
    {
        public struct State : INetworkSerializable, IEquatable<State>
        {
            public int Life;
            public StatusEffect Status;
            public double StatusEnd, StunEnd, SlowEnd;
            public bool Sitting, Lying;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Life);
                serializer.SerializeValue(ref Status);
                serializer.SerializeValue(ref StatusEnd);
                serializer.SerializeValue(ref StunEnd);
                serializer.SerializeValue(ref SlowEnd);
                serializer.SerializeValue(ref Sitting);
                serializer.SerializeValue(ref Lying);
            }
            public bool Equals(State other) => Life == other.Life && Status == other.Status
                && StatusEnd == other.StatusEnd && StunEnd == other.StunEnd && SlowEnd == other.SlowEnd
                && Sitting == other.Sitting && Lying == other.Lying;
        }

        private readonly NetworkVariable<State> state = new();
        private BaseCharacterController character;
        private FamilyController family;
        private float lastStatusEnd, lastStunEnd, lastSlowEnd;

        private void Awake()
        {
            character = GetComponent<BaseCharacterController>();
            family = character as FamilyController;
        }

        public override void OnNetworkSpawn()
        {
            character.NetworkSimulation = true;
            if (IsServer)
            {
                Publish(true);
                if (family != null) family.OnAttackStarted += HandleAttack;
            }
            else
            {
                state.OnValueChanged += HandleChanged;
                Apply(state.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= HandleChanged;
            if (family != null) family.OnAttackStarted -= HandleAttack;
            character.NetworkSimulation = false;
        }

        private void LateUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            character.TickStatusEffects();
            Publish(false);
        }

        private void Publish(bool force)
        {
            var next = state.Value;
            next.Life = character.CurrentLifeSegments;
            next.Status = character.CurrentStatusEffect;
            next.Sitting = character.IsSitting;
            next.Lying = character.IsLying;
            UpdateEnd(character.StatusEffectEndTime, ref lastStatusEnd, ref next.StatusEnd, force);
            UpdateEnd(character.TrapSlowEndTime, ref lastSlowEnd, ref next.SlowEnd, force);
            UpdateEnd(family != null ? family.FlashlightStunEndTime : 0f,
                ref lastStunEnd, ref next.StunEnd, force);
            state.Value = next;
        }

        private void UpdateEnd(float localEnd, ref float previous, ref double serverEnd, bool force)
        {
            if (!force && localEnd == previous) return;
            previous = localEnd;
            serverEnd = localEnd <= Time.time ? 0d : NetworkManager.ServerTime.Time + localEnd - Time.time;
        }

        private void HandleChanged(State previous, State current) => Apply(current);
        private void HandleAttack() => GetComponent<NetworkAnimator>()?.SetTrigger("isAttacking");
        private float Remaining(double end) => (float)Math.Max(0d, end - NetworkManager.ServerTime.Time);
        private void Apply(State current)
        {
            character.ApplyReplicatedState(current.Life, current.Status,
                Remaining(current.StatusEnd), Remaining(current.SlowEnd), current.Sitting, current.Lying);
            if (family != null) family.SynchronizeFlashlightStun(Remaining(current.StunEnd));
        }
    }
}
