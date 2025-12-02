using UnityEngine;

namespace MoreMountains.CorgiEngine
{
    /// <summary>
    /// 击退工具类 - 统一处理击退逻辑
    /// 支持普通状态和摆荡状态的不同处理
    /// </summary>
    public static class KnockbackUtility
    {
        /// <summary>
        /// 击退后冻结水平移动的时间
        /// </summary>
        public const float KNOCKBACK_FREEZE_DURATION = 0.2f;

        /// <summary>
        /// 对目标应用击退力
        /// </summary>
        public static bool ApplyKnockback(
            Collider2D target,
            Vector2 knockbackForce,
            Vector3 knockbackOrigin,
            Vector2? forceDirection = null,
            float swingMultiplier = 1.5f)
        {
            if (target == null) return false;

            // 获取必要组件
            Health health = target.GetComponent<Health>();
            if (health == null) return false;
            
            // 检查是否免疫击退
            if (health.ImmuneToKnockback) return false;

            CorgiController controller = health.AssociatedController;
            if (controller == null)
            {
                controller = target.GetComponent<CorgiController>();
            }
            if (controller == null) return false;

            // 计算击退方向
            Vector2 direction;
            if (forceDirection.HasValue)
            {
                direction = forceDirection.Value.normalized;
            }
            else
            {
                direction = ((Vector2)target.transform.position - (Vector2)knockbackOrigin).normalized;
            }
            
            // 安全检查
            if (float.IsNaN(direction.x) || direction.x == 0)
            {
                direction.x = 1f;
            }
            if (float.IsNaN(direction.y))
            {
                direction.y = 0f;
            }

            // 获取 Character 组件
            Character character = target.GetComponent<Character>();
            if (character == null)
            {
                character = controller.gameObject.GetComponent<Character>();
            }

            // 检查是否在摆荡状态
            CharacterGrapple grapple = character?.FindAbility<CharacterGrapple>();
            
            if (grapple != null && grapple.IsSwinging)
            {
                return ApplySwingKnockback(grapple, direction, knockbackForce, swingMultiplier, knockbackOrigin);
            }
            else
            {
                return ApplyNormalKnockback(controller, character, direction, knockbackForce);
            }
        }

        /// <summary>
        /// 应用普通击退（非摆荡状态）
        /// </summary>
        private static bool ApplyNormalKnockback(
            CorgiController controller,
            Character character,
            Vector2 direction,
            Vector2 knockbackForce)
        {
            // 计算最终击退力
            Vector2 finalForce = new Vector2(
                knockbackForce.x * Mathf.Sign(direction.x),
                knockbackForce.y
            );

            // 如果方向有垂直分量，也考虑它
            if (Mathf.Abs(direction.y) > 0.3f)
            {
                finalForce.y = knockbackForce.y * Mathf.Sign(direction.y);
                if (finalForce.y < 0) finalForce.y = knockbackForce.y * 0.5f;
            }

            // 应用击退
            controller.SetForce(finalForce);

            // 处理跳跃状态
            CharacterJump characterJump = character?.FindAbility<CharacterJump>();
            if (characterJump != null)
            {
                characterJump.SetCanJumpStop(false);
                characterJump.SetJumpFlags();
            }

            // 暂时禁用水平移动输入，防止力被覆盖
            if (character != null)
            {
                KnockbackEffect effect = character.gameObject.GetComponent<KnockbackEffect>();
                if (effect == null)
                {
                    effect = character.gameObject.AddComponent<KnockbackEffect>();
                }
                effect.Apply(KNOCKBACK_FREEZE_DURATION);
            }

            return true;
        }

        /// <summary>
        /// 应用摆荡状态击退（转换为角速度变化）
        /// </summary>
        private static bool ApplySwingKnockback(
            CharacterGrapple grapple,
            Vector2 direction,
            Vector2 knockbackForce,
            float swingMultiplier,
            Vector3 impactOrigin)
        {
            Vector2 impulse = new Vector2(
                knockbackForce.x * Mathf.Sign(direction.x),
                knockbackForce.y
            ) * swingMultiplier;

            return grapple.ApplyExternalImpulse(impulse, impactOrigin);
        }

        /// <summary>
        /// 对范围内所有目标应用击退
        /// </summary>
        public static int ApplyRadialKnockback(
            Vector3 origin,
            float radius,
            Vector2 knockbackForce,
            LayerMask targetLayerMask,
            float damage = 0f,
            GameObject damageInstigator = null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, targetLayerMask);
            int count = 0;

            foreach (var hit in hits)
            {
                if (damage > 0f)
                {
                    Health health = hit.GetComponent<Health>();
                    if (health != null)
                    {
                        health.Damage(damage, damageInstigator, 0f, 0.5f, Vector3.zero, null);
                    }
                }

                if (ApplyKnockback(hit, knockbackForce, origin))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 对范围内所有目标应用击退（带自定义方向）
        /// </summary>
        public static int ApplyDirectionalKnockback(
            Vector3 origin,
            float radius,
            Vector2 knockbackForce,
            Vector2 forceDirection,
            LayerMask targetLayerMask,
            float damage = 0f,
            GameObject damageInstigator = null)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, targetLayerMask);
            int count = 0;

            foreach (var hit in hits)
            {
                if (damage > 0f)
                {
                    Health health = hit.GetComponent<Health>();
                    if (health != null)
                    {
                        health.Damage(damage, damageInstigator, 0f, 0.5f, Vector3.zero, null);
                    }
                }

                if (ApplyKnockback(hit, knockbackForce, origin, forceDirection))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
