using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
    public struct DashChargeChangedEvent
    {
        public int CurrentCharges;
        public int MaxCharges;
        public int PreviousCharges;
        public DashChargeChangeType ChangeType;
        public GameObject Player;
        
        public DashChargeChangedEvent(int current, int max, int previous, DashChargeChangeType type, GameObject player)
        {
            CurrentCharges = current;
            MaxCharges = max;
            PreviousCharges = previous;
            ChangeType = type;
            Player = player;
        }
        
        static DashChargeChangedEvent e;
        public static void Trigger(int current, int max, int previous, DashChargeChangeType type, GameObject player)
        {
            e.CurrentCharges = current;
            e.MaxCharges = max;
            e.PreviousCharges = previous;
            e.ChangeType = type;
            e.Player = player;
            MMEventManager.TriggerEvent(e);
        }
    }
    
    public enum DashChargeChangeType
    {
        Gained,
        Consumed,
        Reset,
        Initialized
    }

    [AddComponentMenu("Corgi Engine/Character/Abilities/Dash Charge Manager")]
    public class DashChargeManager : CharacterAbility, MMEventListener<MMDamageTakenEvent>
    {
        public override string HelpBoxText() { return "管理闪现技能的充能。击杀敌人获得充能，使用Dash消耗充能。"; }

        [Header("=== 充能设置 ===")]
        public int MaxCharges = 3;
        public int ChargesPerKill = 1;
        public int ChargesPerBossKill = 3;
        public int ChargesConsumedPerDash = 0;
        public int InitialCharges = 0;
        public bool RequireChargeToUseDash = true;

        [Header("=== 防重复击杀 ===")]
        public float KillCooldownPerEnemy = 2f;

        [Header("=== Boss识别 ===")]
        public string BossLayerName = "Boss";

        [Header("=== 反馈 ===")]
        public MMFeedbacks ChargeGainedFeedback;
        public MMFeedbacks ChargeFullFeedback;
        public MMFeedbacks ChargeConsumedFeedback;
        public MMFeedbacks ChargeInsufficientFeedback;

        [Header("=== 调试 ===")]
        public bool DebugMode = false;
        
        [MMReadOnly]
        public int CurrentCharges = 0;
        
        [MMReadOnly]
        public int TotalKills = 0;

        public int Charges { get { return CurrentCharges; } }
        public bool IsFullyCharged { get { return CurrentCharges >= MaxCharges; } }
        
        public bool HasSufficientCharge
        {
            get
            {
                if (!RequireChargeToUseDash) return true;
                if (ChargesConsumedPerDash > 0)
                    return CurrentCharges >= ChargesConsumedPerDash;
                return CurrentCharges > 0;
            }
        }
        
        public float ChargePercentage { get { return MaxCharges > 0 ? (float)CurrentCharges / MaxCharges : 0f; } }

        protected int _bossLayerMask;
        protected GameObject _playerGameObject;
        protected bool _initialized = false;
        protected Dictionary<int, float> _killTimestamps = new Dictionary<int, float>();

        protected override void Initialization()
        {
            base.Initialization();
            
            _playerGameObject = gameObject;
            
            int bossLayer = LayerMask.NameToLayer(BossLayerName);
            _bossLayerMask = (bossLayer != -1) ? (1 << bossLayer) : 0;
            
            CurrentCharges = Mathf.Clamp(InitialCharges, 0, MaxCharges);
            _initialized = true;
            
            DashChargeChangedEvent.Trigger(CurrentCharges, MaxCharges, 0, DashChargeChangeType.Initialized, _playerGameObject);
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 初始化完成 - 充能: " + CurrentCharges + "/" + MaxCharges);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            this.MMEventStartListening<MMDamageTakenEvent>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            this.MMEventStopListening<MMDamageTakenEvent>();
        }

        public void OnMMEvent(MMDamageTakenEvent damageTakenEvent)
        {
            if (!_initialized) return;
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 收到MMDamageTakenEvent: Target=" + 
                    (damageTakenEvent.AffectedHealth != null ? damageTakenEvent.AffectedHealth.name : "null") +
                    ", CurrentHealth=" + damageTakenEvent.CurrentHealth +
                    ", Instigator=" + (damageTakenEvent.Instigator != null ? damageTakenEvent.Instigator.name : "null") +
                    ", Frame=" + Time.frameCount);
            
            if (damageTakenEvent.CurrentHealth > 0) return;
            
            Health affectedHealth = damageTakenEvent.AffectedHealth;
            GameObject instigator = damageTakenEvent.Instigator;
            
            if (affectedHealth == null || instigator == null) return;
            if (affectedHealth == _health) return;
            if (!IsEnemy(affectedHealth)) return;
            if (!IsPlayerInstigator(instigator)) return;
            
            int enemyId = affectedHealth.GetInstanceID();
            float currentTime = Time.time;
            
            if (_killTimestamps.TryGetValue(enemyId, out float lastKillTime))
            {
                if (currentTime - lastKillTime < KillCooldownPerEnemy)
                {
                    if (DebugMode)
                        Debug.Log("[DashChargeManager] 敌人 " + affectedHealth.name + " 在冷却中，跳过");
                    return;
                }
            }
            
            _killTimestamps[enemyId] = currentTime;
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 通过所有检查，准备处理击杀: " + affectedHealth.name);
            
            bool isBoss = IsBoss(affectedHealth.gameObject);
            OnEnemyKilled(isBoss, affectedHealth.name);
        }

        protected virtual void OnEnemyKilled(bool isBoss, string enemyName)
        {
            TotalKills++;
            
            int chargeGain = isBoss ? ChargesPerBossKill : ChargesPerKill;
            AddCharges(chargeGain);
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 击杀" + (isBoss ? "Boss" : "敌人") + " " + enemyName + " +" + chargeGain + "充能 = " + CurrentCharges + "/" + MaxCharges);
        }

        public virtual void AddCharges(int amount)
        {
            if (amount <= 0) return;
            
            int previousCharges = CurrentCharges;
            bool wasFullBefore = IsFullyCharged;
            
            CurrentCharges = Mathf.Clamp(CurrentCharges + amount, 0, MaxCharges);
            
            if (CurrentCharges != previousCharges)
            {
                DashChargeChangedEvent.Trigger(CurrentCharges, MaxCharges, previousCharges, DashChargeChangeType.Gained, _playerGameObject);
                if (ChargeGainedFeedback != null) ChargeGainedFeedback.PlayFeedbacks(transform.position);
                
                if (!wasFullBefore && IsFullyCharged)
                {
                    if (ChargeFullFeedback != null) ChargeFullFeedback.PlayFeedbacks(transform.position);
                }
            }
        }

        public virtual bool TryConsumeCharges()
        {
            if (!HasSufficientCharge)
            {
                if (ChargeInsufficientFeedback != null) ChargeInsufficientFeedback.PlayFeedbacks(transform.position);
                if (DebugMode)
                    Debug.Log("[DashChargeManager] 充能不足，无法使用Dash");
                return false;
            }
            
            int previousCharges = CurrentCharges;
            int consumeAmount = ChargesConsumedPerDash <= 0 ? CurrentCharges : ChargesConsumedPerDash;
            CurrentCharges = Mathf.Max(0, CurrentCharges - consumeAmount);
            
            DashChargeChangedEvent.Trigger(CurrentCharges, MaxCharges, previousCharges, DashChargeChangeType.Consumed, _playerGameObject);
            if (ChargeConsumedFeedback != null) ChargeConsumedFeedback.PlayFeedbacks(transform.position);
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 消耗" + consumeAmount + "充能 = " + CurrentCharges + "/" + MaxCharges);
            
            return true;
        }

        public virtual bool CanUseDash()
        {
            return HasSufficientCharge;
        }

        public virtual void ResetCharges()
        {
            int previousCharges = CurrentCharges;
            CurrentCharges = Mathf.Clamp(InitialCharges, 0, MaxCharges);
            DashChargeChangedEvent.Trigger(CurrentCharges, MaxCharges, previousCharges, DashChargeChangeType.Reset, _playerGameObject);
            
            if (DebugMode)
                Debug.Log("[DashChargeManager] 重置充能 = " + CurrentCharges + "/" + MaxCharges);
        }

        public virtual void SetCharges(int amount)
        {
            int previousCharges = CurrentCharges;
            CurrentCharges = Mathf.Clamp(amount, 0, MaxCharges);
            
            if (CurrentCharges != previousCharges)
            {
                var changeType = CurrentCharges > previousCharges ? DashChargeChangeType.Gained : DashChargeChangeType.Consumed;
                DashChargeChangedEvent.Trigger(CurrentCharges, MaxCharges, previousCharges, changeType, _playerGameObject);
            }
        }

        public virtual void FillCharges()
        {
            SetCharges(MaxCharges);
            if (ChargeFullFeedback != null) ChargeFullFeedback.PlayFeedbacks(transform.position);
        }

        protected virtual bool IsPlayerInstigator(GameObject instigator)
        {
            if (instigator == null) return false;
            if (instigator == _playerGameObject) return true;

            DamageOnTouch damageOnTouch = instigator.GetComponent<DamageOnTouch>();
            if (damageOnTouch != null && damageOnTouch.Owner != null)
            {
                if (damageOnTouch.Owner == _playerGameObject) return true;
                Character ownerChar = damageOnTouch.Owner.GetComponent<Character>();
                if (ownerChar != null && ownerChar.CharacterType == Character.CharacterTypes.Player)
                    return true;
            }

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

            MeleeWeapon meleeWeapon = instigator.GetComponent<MeleeWeapon>();
            if (meleeWeapon != null && meleeWeapon.Owner != null)
            {
                if (meleeWeapon.Owner == _playerGameObject) return true;
                Character ownerChar = meleeWeapon.Owner.GetComponent<Character>();
                if (ownerChar != null && ownerChar.CharacterType == Character.CharacterTypes.Player)
                    return true;
            }

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



        public override void ResetAbility()
        {
            base.ResetAbility();
            ResetCharges();
            _killTimestamps.Clear();
        }
    }
}