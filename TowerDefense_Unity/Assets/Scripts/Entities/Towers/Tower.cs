using System;
using System.Collections.Generic;
using System.Linq;
using Game.Entities.EventContainers;
using Game.Entities.Interfaces;
using Game.Entities.MovingEntities;
using UnityEngine;

namespace Game.Entities.Towers
{
	public abstract class TowerBase : Entity, IAggressor
	{
		private float _attackCoolDown = 0.0f;
		private const float ATTACK_COOL_DOWN_DURATION = 3.0f;

        [SerializeField] protected readonly float StartAttackRange;
		[SerializeField] protected readonly float MaxAttackRange;
		[SerializeField] protected float AttackRange;
        [Space]
		[SerializeField] protected TowerAttack Attack;
		[Space]
		[SerializeField] protected IDamageable TargetDamageable;
		[Space]
		[SerializeField] private Allegiance _allegiance;
		[Space]
		[SerializeField] private bool _isDebug;


        /// <inheritdoc />
        /// <summary>
        /// Used to get the attack of this IAggressor.
        /// Returns the attack class of this instance.
        /// </summary>
        /// <returns>Returns the attack class of this instance.</returns>
        public IAttack GetAttack() => Attack;

        /// <inheritdoc />
        /// <summary>
        /// Forcefully override the target of this entity.
        /// Target is set by the aggressor itself, but can
        /// be forcefully overriden at will.
        /// If target is out of range, this will reset to attack targets in range.
        /// </summary>
        /// <param name="target"> Target to focus upon.</param>
        public void SetTarget(in IDamageable target) => TargetDamageable = target;

		private void Update()
		{
			if (_isDebug)
				DrawDebug();

			TargetingAndAttacks();
		}

		private void DrawDebug()
		{
			if (TargetDamageable == null)
				return;

			Debug.DrawLine(GetLocation(), TargetDamageable.GetPosition(), Color.red);
		}

		private void OnDrawGizmos()
		{
			if(!_isDebug)
				return;

			Gizmos.DrawWireSphere(GetLocation(), AttackRange);

			if (TargetDamageable == null)
				return;

			Gizmos.DrawWireSphere(GetLocation(), 1.0f);
		}

        private void TargetingAndAttacks()
		{
			if (TargetDamageable == null)
				GetNewTarget(out TargetDamageable);

			if (_attackCoolDown > 0)
				_attackCoolDown -= Time.unscaledDeltaTime;

			ExecuteAttack();
		}

		private void GetNewTarget(out IDamageable target)
		{
			//TODO: Not quite what we want, large performance impact.
			target = MemoryObjectPool<IDamageable>.Instance.
				Where(x => x.IsConducting() &&
				           x.GetAllegiance() != _allegiance &&
				           Vector3.Distance(GetLocation(), x.GetPosition()) < AttackRange).
				OrderByDescending(x => x.GetPriority()).
				ThenBy(x => Vector3.Distance(GetLocation(), x.GetPosition())).
				FirstOrDefault();

			if(target == null || !_isDebug)
				return;
			
			target.OnDeath += OnTargetDeath;
			target.OnHit += OnTargetHit;
		}

		private void OnTargetHit(in IDamageable sender, in EntityDamaged payload)
		{
			Debug.Log($"Tower [{GetHashCode()}]: Target current health: {payload.Health}");
		}

		private void OnTargetDeath(in IDamageable sender, in EntityDamaged payload)
		{
			Debug.Log($"Tower [{GetHashCode()}]: Target has died.");
			sender.OnDeath -= OnTargetDeath;
			sender.OnHit -= OnTargetHit;
			TargetDamageable = null;
			GetNewTarget(out TargetDamageable);
		}

		/// <inheritdoc />
        /// <summary>
        /// Used to forcefully attack the current target.
        /// If this instance has no target, target is out of
        /// range, or is reloading. This instance will stand idle.
        /// </summary>
        public void ExecuteAttack()
		{
			if (_attackCoolDown > 0 || TargetDamageable == null)
				return;

			if (IsTargetForsaken())
				return;

			_attackCoolDown = ATTACK_COOL_DOWN_DURATION;
			Attack.ExecuteAttack(TargetDamageable, TargetDamageable.GetPosition());
        }

		private bool IsTargetForsaken()
		{
			if (Vector3.Distance(GetLocation(), TargetDamageable.GetPosition()) < AttackRange)
				return false;

			TargetDamageable = null;
			return true;
		}
    }
	public class Tower : TowerBase, IUpgradeable
	{
		[SerializeField] private IUpgrade[] _Upgrades;


		/// <summary>
		/// Increases the range of this turret.
		/// Should only ever be called by an upgrade.
		/// </summary>
		/// <param name="increase"></param>
		public void IncreaseRange(float increase)
		{
			AttackRange += increase;
			AttackRange = Mathf.Clamp(AttackRange, StartAttackRange, MaxAttackRange);
		}

		public void IncreaseDamage(float increase)
		{
			Attack = new TowerAttack(80);
		}

		/// <inheritdoc />
		/// <summary>
		/// Upgrades the instance by T.
		/// T must be gotten via <see cref="GetPossibleUpgrades"/>
		/// Upgrade can be rejected if not enough resources are available.
		/// </summary>
		/// <param name="upgrade"> Upgrade to apply to this instance.</param>
		public void Upgrade(in IUpgrade upgrade)
		{
			if (!ResourceSystem.Instance.RunTransaction(upgrade.GetCost()))
				return;

			upgrade.ApplyUpgrade(this);
		}

		/// <inheritdoc />
		/// <summary>
		/// Returns an array of T containing all
		/// upgrades of T. If an upgrade is applied it will
		/// be removed from this list.
		/// Will never be null.
		/// </summary>
		/// <returns> An array of T with all possible upgrades. Never null.</returns>
		public void GetPossibleUpgrades(out IUpgrade[] upgrades)
		{
			if (_Upgrades == null)
			{
				upgrades = new IUpgrade[0];
				return;
			}

			upgrades = _Upgrades;
		}
	}

	[CreateAssetMenu(fileName = "TowerAttack", menuName = "Tower/Tower Attack", order = 1)]
	public class TowerAttack : ScriptableObject, IAttack
	{
		[SerializeField] private AttackType _attackType;
		[SerializeField] private float _areaOfEffect = -1.0f;
		private readonly AttackEffects _attackEffects;
		public AttackType GetAttackType() => AttackType.AREA_OF_EFFECT;

		public float GetAreaOfEffect() => 10.0f;

		public float GetDamage() => _attackEffects.GetDamage();

		public TowerAttack(float damage = 60) =>
			_attackEffects = new AttackEffects(damage, new[] {StatusEffects.NONE});

		public void ExecuteAttack(in IDamageable damageable, Vector3? position = null)
		{
			if (position != null && _areaOfEffect > 0)
			{
				AreaAttack(Allegiance.FRIENDLY, position.Value);
				return;
			}

			damageable.ApplyOnHitEffects(_attackEffects);
		}

		private void AreaAttack(Allegiance allegiance, Vector3 position)
		{
			foreach (IDamageable damageable in MemoryObjectPool<IDamageable>.Instance.Where(x =>
				x.GetAllegiance() != allegiance &&
				Vector3.Distance(x.GetPosition(), position) < _areaOfEffect))
				damageable.ApplyOnHitEffects(_attackEffects);
		}
	}
}
