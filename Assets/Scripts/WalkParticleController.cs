using UnityEngine;

public class WalkParticleController : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem walkParticles;
    
    [Header("Emission Settings")]
    public int particlesPerStep = 1;
    public float emissionInterval = 0.5f;
    public int dashParticleAmount = 5;
    
    [Header("Emission Angle Randomness")]
    [Range(0f, 15f)]
    public float angleRandomness = 5f;
    [Range(20f, 60f)]
    public float emissionAngle = 35f;
    
    [Header("Surface Colors")]
    public Color defaultSurfaceColor = new Color(0.627f, 0.925f, 0.424f, 1f);
    public SurfaceColorMapping[] surfaceColors;
    
    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    
    float emissionTimer;
    ParticleSystem.MainModule mainModule;
    ParticleSystem.ShapeModule shapeModule;
    SpriteRenderer playerSprite;
    
    void Start()
    {
        if (walkParticles != null)
        {
            mainModule = walkParticles.main;
            shapeModule = walkParticles.shape;
            walkParticles.Stop();
        }
        
        playerSprite = GetComponentInParent<SpriteRenderer>();
    }
    
    public void EmitParticles(float velocityX, bool isDashing = false)
    {
        if (walkParticles == null) return;
        
        if (isDashing)
        {
            EmitDashParticles(velocityX);
            return;
        }
        
        emissionTimer += Time.deltaTime;
        
        if (emissionTimer >= emissionInterval)
        {
            emissionTimer = 0f;
            EmitWalkParticles(velocityX);
        }
    }
    
    void EmitWalkParticles(float velocityX)
    {
        Color surfaceColor = GetSurfaceColor();
        mainModule.startColor = surfaceColor;
        EmitKicked(velocityX, particlesPerStep, surfaceColor);
    }

    void EmitDashParticles(float velocityX)
    {
        Color surfaceColor = GetSurfaceColor();
        mainModule.startColor = surfaceColor;
        EmitKicked(velocityX, dashParticleAmount, surfaceColor);
    }

    /// <summary>
    /// Emits particles with an explicit world-space velocity so they arc backward and upward —
    /// as if kicked off the ground by the player's foot.
    /// </summary>
    void EmitKicked(float velocityX, int count, Color color)
    {
        // Kick direction: opposite to movement, angled upward
        bool movingRight = playerSprite != null ? !playerSprite.flipX : velocityX > 0;
        float kickX = movingRight ? -1f : 1f;   // fire backward

        for (int i = 0; i < count; i++)
        {
            ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();

            float speedH = Random.Range(1.2f, 3.0f);
            float speedV = Random.Range(1.5f, 3.5f);
            ep.velocity    = new Vector3(kickX * speedH, speedV, 0f);
            ep.startColor  = color;
            ep.startSize   = Random.Range(0.04f, 0.1f);
            ep.startLifetime = Random.Range(0.25f, 0.55f);

            walkParticles.Emit(ep, 1);
        }
    }
    
    Color GetSurfaceColor()
    {
        if (groundCheck == null) return defaultSurfaceColor;
        
        RaycastHit2D hit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckRadius * 2f,
            groundLayer
        );
        
        if (hit.collider != null)
        {
            string surfaceTag = hit.collider.tag;
            
            foreach (var mapping in surfaceColors)
            {
                if (mapping.surfaceTag == surfaceTag)
                {
                    return mapping.particleColor;
                }
            }
            
            SpriteRenderer sr = hit.collider.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                Texture2D texture = sr.sprite.texture;
                if (texture != null && texture.isReadable)
                {
                    Color averageColor = GetAverageColor(texture);
                    return new Color(averageColor.r, averageColor.g, averageColor.b, 1f);
                }
            }
        }
        
        return defaultSurfaceColor;
    }
    
    Color GetAverageColor(Texture2D texture)
    {
        Color[] pixels = texture.GetPixels();
        float r = 0, g = 0, b = 0;
        int count = 0;
        
        for (int i = 0; i < pixels.Length; i += 10)
        {
            if (pixels[i].a > 0.1f)
            {
                r += pixels[i].r;
                g += pixels[i].g;
                b += pixels[i].b;
                count++;
            }
        }
        
        if (count > 0)
        {
            return new Color(r / count, g / count, b / count, 1f);
        }
        
        return defaultSurfaceColor;
    }
}

[System.Serializable]
public class SurfaceColorMapping
{
    public string surfaceTag = "Untagged";
    public Color particleColor = Color.white;
}
