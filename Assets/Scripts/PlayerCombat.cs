using System.Collections;
using UnityEngine;

/// <summary>
/// Handles player sword attacks. Left-click must land on an enemy collider within attackRange.
/// Requires isHoldingSword (Bool) and isAttacking (Trigger) animator parameters.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private float attackRange    = 2.2f;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private float knockbackForce = 6f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private float damageDelay    = 0.15f;

    // Radius around the click point used to snap onto enemies (forgiveness margin)
    private const float ClickTolerance = 0.35f;

    private static readonly int IsHoldingSwordHash = Animator.StringToHash("isHoldingSword");
    private static readonly int IsAttackingHash    = Animator.StringToHash("isAttacking");

    private ItemData   equippedItem;
    private bool       isAttacking;
    private float      cooldown;
    private Animator   animator;
    private SpriteRenderer sr;

    void Start()
    {
        animator = GetComponent<Animator>();
        sr       = GetComponent<SpriteRenderer>();
        Inventory.Instance?.OnItemEquipped.AddListener(OnItemEquipped);
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;

        if (equippedItem != null && !isAttacking && cooldown <= 0f && Input.GetMouseButtonDown(0))
            StartCoroutine(Attack());
    }

    IEnumerator Attack()
    {
        isAttacking = true;
        cooldown    = attackCooldown;
        animator?.SetTrigger(IsAttackingHash);

        // Sample click position at the moment of input, before the delay
        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        yield return new WaitForSeconds(damageDelay);

        // Check whether the click landed on or near an enemy within weapon range
        Collider2D hit = Physics2D.OverlapCircle(mouseWorld, ClickTolerance, enemyLayers);
        if (hit != null && hit.gameObject != gameObject)
        {
            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist <= attackRange)
            {
                Health hp = hit.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                {
                    hp.TakeDamage(equippedItem.damage);

                    // Notify ZombieAI so it can pause movement and not cancel the knockback
                    ZombieAI zombie = hit.GetComponent<ZombieAI>();
                    if (zombie != null)
                        zombie.Knockback((hit.transform.position - transform.position).normalized * knockbackForce);
                    else
                    {
                        Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
                        rb?.AddForce((hit.transform.position - transform.position).normalized * knockbackForce, ForceMode2D.Impulse);
                    }

                    HitSpark.Spawn(mouseWorld);
                }
            }
        }

        yield return new WaitForSeconds(attackCooldown - damageDelay);
        isAttacking = false;
    }

    /// <summary>Called by Inventory when the player equips or unequips an item.</summary>
    void OnItemEquipped(ItemData item)
    {
        equippedItem = item;
        if (equippedItem == null) { StopAllCoroutines(); isAttacking = false; }
        animator?.SetBool(IsHoldingSwordHash, equippedItem != null);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
