using UnityEngine;
using System.Collections;
using MoreMountains.Tools;
using System.Collections.Generic;

namespace MoreMountains.CorgiEngine
{
	/// <summary>
	/// Add this component to an object containing multiple weapons and it'll turn it into a ComboWeapon, allowing you to chain attacks from all the different weapons
	/// </summary>
	[AddComponentMenu("Corgi Engine/Weapons/Combo Weapon")]
	public class ComboWeapon : CorgiMonoBehaviour
	{
		[Header("Combo")]

		/// whether or not the combo can be dropped if enough time passes between two consecutive attacks
		[Tooltip("whether or not the combo can be dropped if enough time passes between two consecutive attacks")]
		public bool DroppableCombo = true;
		/// the delay after which the combo drops
		[Tooltip("the delay after which the combo drops")]
		public float DropComboDelay = 0.5f;

		[Header("Kill Based Combo")]
		
		/// if true, the combo can only continue to the next weapon if the player killed an enemy
		[Tooltip("if true, the combo can only continue to the next weapon if the player killed an enemy")]
		public bool RequireKillToContinueCombo = false;
		
		/// the cooldown duration (in seconds) to apply if no kill was made during the attack
		[Tooltip("the cooldown duration (in seconds) to apply if no kill was made during the attack")]
		[MMCondition("RequireKillToContinueCombo", true)]
		public float NoKillCooldownDuration = 3f;
		
		/// if true, hitting a damageable (but not killing) also allows combo to continue
		[Tooltip("if true, hitting a damageable (but not killing) also allows combo to continue")]
		[MMCondition("RequireKillToContinueCombo", true)]
		public bool AllowComboOnHitDamageable = false;


		[Header("Animation")]

		/// the animation parameter's name that should turn true when a combo is in progress
		[Tooltip("the animation parameter's name that should turn true when a combo is in progress")]
		public string ComboInProgressAnimationParameter = "ComboInProgress";

		[Header("Debug")]

		/// the list of weapons, set automatically by the class
		[MMReadOnly]
		[Tooltip("the list of weapons, set automatically by the class")]
		public Weapon[] Weapons;
		/// the reference to the weapon's Owner
		[MMReadOnly]
		[Tooltip("the reference to the weapon's Owner")]
		public CharacterHandleWeapon OwnerCharacterHandleWeapon;
		/// the time spent since the last weapon stopped
		[MMReadOnly]
		[Tooltip("the time spent since the last weapon stopped")]
		public float TimeSinceLastWeaponStopped;
        
		/// <summary>
		/// True if a combo is in progress, false otherwise
		/// </summary>
		/// <returns></returns>
		public bool ComboInProgress
		{
			get
			{
				bool comboInProgress = false;
				foreach (Weapon weapon in Weapons)
				{
					if (weapon.WeaponState.CurrentState != Weapon.WeaponStates.WeaponIdle)
					{
						comboInProgress = true;
					}
				}
				return comboInProgress;
			}
		}

		protected int _currentWeaponIndex = 0;
		protected bool _countdownActive = false;
		protected bool _killRegisteredThisAttack = false;
		protected bool _hitDamageableRegisteredThisAttack = false;
		protected bool _inCooldownPenalty = false;
		protected float _cooldownPenaltyStartTime = 0f;


		/// <summary>
		/// On start we initialize our Combo Weapon
		/// </summary>
		protected virtual void Start()
		{
			Initialization();
		}

		/// <summary>
		/// Grabs all Weapon components and initializes them
		/// </summary>
		public virtual void Initialization()
		{
			Weapons = GetComponents<Weapon>();
			InitializeUnusedWeapons();
		}

		/// <summary>
		/// Called when a kill is registered from one of the weapons
		/// </summary>
		public virtual void OnKillRegistered()
		{
			_killRegisteredThisAttack = true;
		}

		/// <summary>
		/// Called when a hit on a damageable is registered from one of the weapons
		/// </summary>
		public virtual void OnHitDamageableRegistered()
		{
			_hitDamageableRegisteredThisAttack = true;
		}

		/// <summary>
		/// Applies a cooldown penalty when no kill was made, blocking all attacks for NoKillCooldownDuration
		/// </summary>
		protected virtual void ApplyCooldownPenalty()
		{
			_inCooldownPenalty = true;
			_cooldownPenaltyStartTime = Time.time;
			_countdownActive = false;
			
			// Disable all weapons during cooldown
			foreach (Weapon weapon in Weapons)
			{
				weapon.enabled = false;
			}
			
			// Reset to first weapon
			_currentWeaponIndex = 0;
		}

		/// <summary>
		/// Checks if cooldown penalty has expired and re-enables the first weapon
		/// </summary>
		protected virtual void HandleCooldownPenalty()
		{
			if (_inCooldownPenalty)
			{
				if (Time.time - _cooldownPenaltyStartTime >= NoKillCooldownDuration)
				{
					_inCooldownPenalty = false;
					// Re-enable the first weapon after cooldown
					Weapons[0].enabled = true;
					if (OwnerCharacterHandleWeapon != null)
					{
						OwnerCharacterHandleWeapon.CurrentWeapon = Weapons[0];
						OwnerCharacterHandleWeapon.ChangeWeapon(Weapons[0], Weapons[0].WeaponID, true);
					}
				}
			}
		}

		/// <summary>
		/// Returns whether the weapon is currently in cooldown penalty
		/// </summary>
		public virtual bool InCooldownPenalty()
		{
			return _inCooldownPenalty;
		}

		/// <summary>
		/// Returns the remaining cooldown penalty time
		/// </summary>
		public virtual float GetRemainingCooldownPenalty()
		{
			if (!_inCooldownPenalty) return 0f;
			return Mathf.Max(0f, NoKillCooldownDuration - (Time.time - _cooldownPenaltyStartTime));
		}


		/// <summary>
		/// On Update we reset our combo if needed
		/// </summary>
/// <summary>
		/// On Update we reset our combo if needed and handle cooldown penalty
		/// </summary>
		protected virtual void Update()
		{
			HandleCooldownPenalty();
			ResetCombo();
		}

		/// <summary>
		/// Resets the combo if enough time has passed since the last attack
		/// </summary>
		public virtual void ResetCombo()
		{
			if (Weapons.Length > 1)
			{
				if (_countdownActive && DroppableCombo)
				{
					TimeSinceLastWeaponStopped += Time.deltaTime;
					if (TimeSinceLastWeaponStopped > DropComboDelay)
					{
						_countdownActive = false;
						Weapons[_currentWeaponIndex].enabled = false;
						_currentWeaponIndex = 0;
						OwnerCharacterHandleWeapon.CurrentWeapon = Weapons[_currentWeaponIndex];
						OwnerCharacterHandleWeapon.ChangeWeapon(Weapons[_currentWeaponIndex], Weapons[_currentWeaponIndex].WeaponID, true);
						Weapons[_currentWeaponIndex].enabled = true;
					}
				}
			}
		}

		/// <summary>
		/// When one of the weapons get used we turn our countdown off
		/// </summary>
		/// <param name="weaponThatStarted"></param>
/// <summary>
		/// When one of the weapons get used we turn our countdown off and reset kill tracking
		/// </summary>
		/// <param name="weaponThatStarted"></param>
		public virtual void WeaponStarted(Weapon weaponThatStarted)
		{
			_countdownActive = false;
			// Reset kill tracking for this attack
			_killRegisteredThisAttack = false;
			_hitDamageableRegisteredThisAttack = false;
		}

		/// <summary>
		/// When one of the weapons has ended its attack, we start our countdown and switch to the next weapon
		/// </summary>
		/// <param name="weaponThatStopped"></param>
/// <summary>
		/// When one of the weapons has ended its attack, we start our countdown and switch to the next weapon
		/// If RequireKillToContinueCombo is enabled, we check if a kill was made before allowing combo to continue
		/// </summary>
		/// <param name="weaponThatStopped"></param>
		public virtual void WeaponStopped(Weapon weaponThatStopped)
		{
			OwnerCharacterHandleWeapon = Weapons[_currentWeaponIndex].CharacterHandleWeapon;
            
			int newIndex = 0;
			if (OwnerCharacterHandleWeapon != null)
			{
				if (Weapons.Length > 1)
				{
					// Check if we should block combo due to no kill
					if (RequireKillToContinueCombo)
					{
						bool canContinue = _killRegisteredThisAttack || (AllowComboOnHitDamageable && _hitDamageableRegisteredThisAttack);
						
						if (!canContinue)
						{
							// Block combo and apply cooldown penalty
							ApplyCooldownPenalty();
							return;
						}
					}
					
					if (_currentWeaponIndex < Weapons.Length-1)
					{
						newIndex = _currentWeaponIndex + 1;
					}
					else
					{
						newIndex = 0;
					}

					_countdownActive = true;
					TimeSinceLastWeaponStopped = 0f;

					Weapons[_currentWeaponIndex].SetCooldownStartAt();
					Weapons[_currentWeaponIndex].enabled = false;
					_currentWeaponIndex = newIndex;
					OwnerCharacterHandleWeapon.CurrentWeapon = Weapons[newIndex];
					OwnerCharacterHandleWeapon.ChangeWeapon(Weapons[newIndex], Weapons[newIndex].WeaponID, true);
					Weapons[newIndex].enabled = true;
				}
			}
		}

		/// <summary>
		/// Flips all unused weapons so they remain properly oriented
		/// </summary>
		public virtual void FlipUnusedWeapons()
		{
			for (int i = 0; i < Weapons.Length; i++)
			{
				if (i != _currentWeaponIndex)
				{
					Weapons[i].Flipped = !Weapons[i].Flipped;
				}                
			}
		}

		/// <summary>
		/// Initializes all unused weapons
		/// </summary>
		protected virtual void InitializeUnusedWeapons()
		{
			for (int i = 0; i < Weapons.Length; i++)
			{
				if (i != _currentWeaponIndex)
				{
					Weapons[i].enabled = false;
					Weapons[i].SetOwner(Weapons[_currentWeaponIndex].Owner, Weapons[_currentWeaponIndex].CharacterHandleWeapon);
					Weapons[i].Initialization();
				}
			}
			for (int i = 0; i < Weapons.Length; i++)
			{
				if (i == _currentWeaponIndex)
				{
					Weapons[i].enabled = true;
				}
			}
		}
	}
}