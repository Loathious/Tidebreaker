using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// A breakable diamond rock in the Cave level.
/// Requires 3 hits to break. Drops a diamond (adds to inventory) when destroyed.
/// Shows floating "+1 Diamond" text on hit.
/// </summary>
public class DiamondRock : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int   hitsToBreak = 3;
    [SerializeField] private Color intactColor = new Color(0.3f, 0.6f, 1f);
    [SerializeField] private Color crackedColor = new Color(0.2f, 0.4f, 0.8f);
    [SerializeField] private Color breakingColor = new Color(0.15f, 0.25f, 0.6f);

    [Header("Item")]
    [SerializeField] private ItemData diamondItem;   // assign in inspector

    [Header("Interact Hint")]
    [Tooltip("How close the player needs to be to see the 'Attack to mine' hint")]
    [SerializeField] private float hintRange = 2.5f;

    private SpriteRenderer _sr;
    private int            _hitsLeft;
    private bool           _broken;
    private Transform      _player;

    // Simple world-space text label
    private TextMeshPro _hint;

    void Awake()
    {
        _sr       = GetComponent<SpriteRenderer>();
        _hitsLeft = hitsToBreak;
    }

    void Start()
    {
        if (_sr != null) _sr.color = intactColor;

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) _player = p.transform;

        BuildHint();
    }

    void Update()
    {
        if (_hint == null || _player == null) return;
        float dist = Vector2.Distance(transform.position, _player.position);
        _hint.gameObject.SetActive(!_broken && dist < hintRange);
    }

    /// <summary>Called by PlayerCombat when the attack hitbox overlaps this collider.</summary>
    public void Hit()
    {
        if (_broken) return;

        _hitsLeft--;
        StartCoroutine(HitShake());

        UpdateVisual();

        if (_hitsLeft <= 0)
            Break();
    }

    private void UpdateVisual()
    {
        if (_sr == null) return;
        if      (_hitsLeft >= hitsToBreak)     _sr.color = intactColor;
        else if (_hitsLeft == hitsToBreak - 1) _sr.color = crackedColor;
        else                                    _sr.color = breakingColor;
    }

    private void Break()
    {
        _broken = true;
        if (_hint != null) _hint.gameObject.SetActive(false);

        // Give diamond to inventory
        if (diamondItem != null)
            Inventory.Instance?.AddItem(diamondItem);

        // Floating text
        SpawnText("+1 Diamond", Color.cyan);

        // Cyan shard burst
        SpawnShardBurst();

        // Check cave objective
        CaveManager.Instance?.OnDiamondCollected();

        StartCoroutine(BreakEffect());
    }

    /// <summary>Spawns 8 little cyan square shards that fly outward and fade.</summary>
    private void SpawnShardBurst()
    {
        for (int i = 0; i < 8; i++)
        {
            GameObject shard = new GameObject("DiamondShard");
            shard.transform.position = transform.position;

            var sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = MakeShardSprite();
            sr.color = new Color(0.5f, 0.95f, 1f);
            sr.sortingOrder = 6;

            float angle = (i / 8f) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            Vector2 vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2.5f, 4.5f);
            StartCoroutine(AnimateShard(shard, sr, vel));
        }
    }

    private static Sprite _shardSprite;
    private static Sprite MakeShardSprite()
    {
        if (_shardSprite != null) return _shardSprite;
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color32 white = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < 16; i++) pixels[i] = white;
        tex.SetPixels32(pixels);
        tex.Apply();
        _shardSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
        return _shardSprite;
    }

    private IEnumerator AnimateShard(GameObject shard, SpriteRenderer sr, Vector2 velocity)
    {
        float t = 0f;
        float life = 0.55f;
        Vector3 gravity = new Vector3(0f, -6f, 0f);
        Vector3 pos = shard.transform.position;
        Vector3 vel = velocity;
        while (t < life)
        {
            t += Time.deltaTime;
            vel += gravity * Time.deltaTime;
            pos += vel * Time.deltaTime;
            shard.transform.position = pos;
            shard.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.2f, t / life);
            Color c = sr.color; c.a = 1f - (t / life); sr.color = c;
            yield return null;
        }
        Destroy(shard);
    }

    private IEnumerator BreakEffect()
    {
        if (_sr != null)
        {
            for (int i = 0; i < 5; i++)
            {
                _sr.color = Color.white;
                yield return new WaitForSeconds(0.05f);
                _sr.color = breakingColor;
                yield return new WaitForSeconds(0.05f);
            }
        }
        Destroy(gameObject);
    }

    private IEnumerator HitShake()
    {
        Vector3 orig = transform.localPosition;
        for (int i = 0; i < 3; i++)
        {
            transform.localPosition = orig + new Vector3(Random.Range(-0.08f, 0.08f), 0, 0);
            yield return new WaitForSeconds(0.04f);
        }
        transform.localPosition = orig;
    }

    private void SpawnText(string msg, Color col)
    {
        GameObject go = new GameObject("FloatText");
        go.transform.position = transform.position + Vector3.up * 0.6f;
        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text      = msg;
        tmp.color     = col;
        tmp.fontSize  = 3f;
        tmp.alignment = TextAlignmentOptions.Center;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) tmp.font = font;

        // Animate float upward then destroy
        StartCoroutine(FloatAndFade(go, tmp));
    }

    private IEnumerator FloatAndFade(GameObject go, TextMeshPro tmp)
    {
        float t = 0f;
        Vector3 start = go.transform.position;
        while (t < 1f)
        {
            t += Time.deltaTime * 1.2f;
            go.transform.position = start + Vector3.up * t * 0.8f;
            Color c = tmp.color; c.a = 1f - t; tmp.color = c;
            yield return null;
        }
        Destroy(go);
    }

    private void BuildHint()
    {
        GameObject hintGO = new GameObject("MineHint");
        hintGO.transform.SetParent(transform);
        hintGO.transform.localPosition = Vector3.up * 0.8f;
        hintGO.SetActive(false);

        _hint = hintGO.AddComponent<TextMeshPro>();
        _hint.text      = "Attack to mine";
        _hint.fontSize  = 2f;
        _hint.color     = new Color(1f, 1f, 0.5f);
        _hint.alignment = TextAlignmentOptions.Center;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/PressStart2P-Regular SDF")
                          ?? Resources.Load<TMP_FontAsset>("PressStart2P-Regular SDF");
        if (font != null) _hint.font = font;
    }
}
