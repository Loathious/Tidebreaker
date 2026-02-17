using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class BloodParticleSetup : MonoBehaviour
{
    void Awake()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.545f, 0f, 0f, 1f);
        main.gravityModifier = 0.8f;
        main.maxParticles = 50;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;
        
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.545f, 0f, 0f), 0f), new GradientColorKey(new Color(0.545f, 0f, 0f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        Debug.Log("Blood particle system configured!");
    }
}
