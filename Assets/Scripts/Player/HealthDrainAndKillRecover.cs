using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;

namespace MoreMountains.CorgiEngine
{
    [AddComponentMenu("Corgi Engine/Character/Abilities/Health Drain And Kill Recover")]
    public class HealthDrainAndKillRecover : MMMonoBehaviour, MMEventListener<MMDamageTakenEvent>
    {
        [Header("生命流失设置")]
        public bool EnableHealthDrain = true;
        public float HealthDrainPerSecond = 2f;
        public float DrainInterval = 1f;
        [Range(0f, 1f)] public float MinHealthPercentageToStopDrain = 0.1f;

        [Header("击杀回复设置")]
        public bool EnableKillRecovery = true;
        public float HealthRecoverOnKill = 15f;
        public float HealthRecoverOnBossKill = 50f;
        public string BossLayerName = "Boss";

        [Header("反馈设置")]
        public MoreMountains.Feedbacks.MMFeedbacks DrainFeedbacks;
        public MoreMountains.Feedbacks.MMFeedbacks RecoverFeedbacks;

        [Header("调试")]
        public bool EnableDebugLog = false;
        [MMReadOnly] public float TimeSinceLastDrain = 0f;
        [MMReadOnly] public int TotalKills = 0;
        [MMReadOnly] public float TotalHealthRecovered = 0f;

        [Header("安全区")]
        [MMReadOnly] public int SafeZoneCount = 0;
        public bool IsInSafeZone => SafeZoneCount > 0;


        protected Health _health;
        protected Character _character;
        protected bool _initialized = false;
        protected int _bossLayerMask;
        protected float _cachedMinHealth;
        protected float _cachedDrainAmount;
        protected GameObject _playerGameObject;

        protected virtual void Awake()
        {
            _health = GetComponent<Health>();
            _character = GetComponent<Character>();
            _playerGameObject = gameObject;
        }

        protected virtual void Start()
        {
            if (_health == null)
            {
                Debug.LogError("[HealthDrainAndKillRecover] Health组件未找到");
                enabled = false;
                return;
            }

            int bossLayer = LayerMask.NameToLayer(BossLayerName);
            _bossLayerMask = (bossLayer != -1) ? (1 << bossLayer) : 0;
            
            RecalculateCachedValues();
            _initialized = true;
        }

        public virtual void RecalculateCachedValues()
        {
            if (_health != null)
                _cachedMinHealth = _health.MaximumHealth * MinHealthPercentageToStopDrain;
            _cachedDrainAmount = HealthDrainPerSecond * DrainInterval;
        }

        protected virtual void Update()
        {
            if (!_initialized || !EnableHealthDrain) return;
            if (IsInSafeZone) return;

            if (!IsAlive()) return;
            
            TimeSinceLastDrain += Time.deltaTime;
            if (TimeSinceLastDrain < DrainInterval) return;
            
            TimeSinceLastDrain -= DrainInterval;
            
            if (_health.CurrentHealth <= _cachedMinHealth) return;

            float actualDrain = _cachedDrainAmount;
            float newHealth = _health.CurrentHealth - actualDrain;
            
            if (newHealth < _cachedMinHealth)
            {
                actualDrain = _health.CurrentHealth - _cachedMinHealth;
                newHealth = _cachedMinHealth;
            }

            if (actualDrain <= 0) return;

            _health.SetHealth(newHealth, _playerGameObject);

            if (DrainFeedbacks != null)
                DrainFeedbacks.PlayFeedbacks(transform.position);

            if (newHealth <= 0)
                _health.Kill();
        }

        protected virtual void OnEnable()
        {
            this.MMEventStartListening<MMDamageTakenEvent>();
        }

        protected virtual void OnDisable()
        {
            this.MMEventStopListening<MMDamageTakenEvent>();
        }

        public void OnMMEvent(MMDamageTakenEvent damageTakenEvent)
        {
            if (!EnableKillRecovery || !_initialized) return;
            if (damageTakenEvent.CurrentHealth > 0) return;
            
            Health affectedHealth = damageTakenEvent.AffectedHealth;
            GameObject instigator = damageTakenEvent.Instigator;
            
            if (affectedHealth == null || instigator == null) return;
            if (affectedHealth == _health) return;
            if (!IsEnemy(affectedHealth)) return;
            if (!IsPlayerInstigator(instigator)) return;

            bool isBoss = IsBoss(affectedHealth.gameObject);
            RecoverHealth(isBoss);
        }

        protected virtual bool IsPlayerInstigator(GameObject instigator)
        {
            if (instigator == null) return false;
            if (instigator == _playerGameObject) return true;

            // 检查 DamageOnTouch
            DamageOnTouch damageOnTouch = instigator.GetComponent<DamageOnTouch>();
            if (damageOnTouch != null && damageOnTouch.Owner != null)
            {
                if (damageOnTouch.Owner == _playerGameObject) return true;
                Character ownerChar = damageOnTouch.Owner.GetComponent<Character>();
                if (ownerChar != null && ownerChar.CharacterType == Character.CharacterTypes.Player)
                    return true;
            }

            // 检查 Projectile
            Projectile projectile = instigator.GetComponent<Projectile>();
            if (projectile != null)
            {
                GameObject owner = projectile.GetOwner();
                if (owner != null)
                {
                    if (owner == _playerGameObject) return true;
                    Character ownerChar = owner.GetComponent<Character>();
                    if (ownerChar != null && ownerChar.CharacterType == Character.CharacterTypes.Player)
                        return true;
                }
            }

            // 检查 MeleeWeapon
            MeleeWeapon meleeWeapon = instigator.GetComponent<MeleeWeapon>();
            if (meleeWeapon != null && meleeWeapon.Owner != null)
            {
                if (meleeWeapon.Owner == _playerGameObject) return true;
                Character ownerChar = meleeWeapon.Owner.GetComponent<Character>();
                if (ownerChar != null && ownerChar.CharacterType == Character.CharacterTypes.Player)
                    return true;
            }

            // 检查 instigator 本身
            Character instigatorChar = instigator.GetComponent<Character>();
            if (instigatorChar != null && instigatorChar.CharacterType == Character.CharacterTypes.Player)
                return true;
            
            return false;
        }

        protected virtual bool IsEnemy(Health targetHealth)
        {
            if (targetHealth == null) return false;
            Character targetChar = targetHealth.GetComponent<Character>();
            return targetChar != null && targetChar.CharacterType != Character.CharacterTypes.Player;
        }

        protected virtual bool IsBoss(GameObject target)
        {
            if (_bossLayerMask == 0) return false;
            return ((1 << target.layer) & _bossLayerMask) != 0;
        }

        protected virtual void RecoverHealth(bool isBoss)
        {
            if (_health == null || !IsAlive()) return;

            float amount = isBoss ? HealthRecoverOnBossKill : HealthRecoverOnKill;
            _health.GetHealth(amount, _playerGameObject);
            
            TotalKills++;
            TotalHealthRecovered += amount;

            if (RecoverFeedbacks != null)
                RecoverFeedbacks.PlayFeedbacks(transform.position);

            if (EnableDebugLog)
                Debug.Log("[击杀回血] +" + amount + " HP | " + _health.CurrentHealth + "/" + _health.MaximumHealth);
        }

        protected virtual bool IsAlive()
        {
            if (_character == null) return _health != null && _health.CurrentHealth > 0;
            return _character.ConditionState.CurrentState != CharacterStates.CharacterConditions.Dead;
        }

        public virtual void ManualRecover(float amount)
        {
            if (_health == null || amount <= 0) return;
            _health.GetHealth(amount, _playerGameObject);
            TotalHealthRecovered += amount;
            if (RecoverFeedbacks != null)
                RecoverFeedbacks.PlayFeedbacks(transform.position);
        }

        public virtual void ResetStats()
        {
            TimeSinceLastDrain = 0f;
            TotalKills = 0;
            TotalHealthRecovered = 0f;
        }

        public virtual void SetDrainEnabled(bool value) { EnableHealthDrain = value; }
        public virtual void SetKillRecoveryEnabled(bool value) { EnableKillRecovery = value; }
        public virtual void SetDrainRate(float value) { HealthDrainPerSecond = value; RecalculateCachedValues(); }
        public virtual void SetKillRecoveryAmount(float value) { HealthRecoverOnKill = value; }

        /// <summary>
        /// 进入安全区时调用
        /// </summary>
        public virtual void EnterSafeZone()
        {
            SafeZoneCount++;
            if (EnableDebugLog)
                Debug.Log($"[SafeZone] 进入安全区, count={SafeZoneCount}");
        }

        /// <summary>
        /// 离开安全区时调用
        /// </summary>
        public virtual void ExitSafeZone()
        {
            SafeZoneCount = Mathf.Max(0, SafeZoneCount - 1);
            if (EnableDebugLog)
                Debug.Log($"[SafeZone] 离开安全区, count={SafeZoneCount}");
        }

        /// <summary>
        /// 重置安全区计数（用于重生等场景）
        /// </summary>
        public virtual void ResetSafeZoneCount()
        {
            SafeZoneCount = 0;
        }

    }
}
