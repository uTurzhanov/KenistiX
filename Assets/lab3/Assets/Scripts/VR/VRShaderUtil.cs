using UnityEngine;

namespace RayOptics.VR
{
    /// <summary>
    /// Resolves a usable shader for procedurally-created VR button materials,
    /// regardless of which render pipeline the project uses.
    ///
    /// The problem this solves
    /// ────────────────────────
    /// <c>Shader.Find("Standard")</c> only succeeds in the Built-in Render Pipeline.
    /// In URP or HDRP projects that shader does not exist, <c>Shader.Find</c> returns
    /// null, and <c>new Material(null)</c> silently falls back to Unity's bright
    /// magenta/pink error shader — which is exactly the "pure purple" symptom seen
    /// in Play mode.
    ///
    /// This utility tries a list of shader names that cover Built-in, URP, and HDRP,
    /// in order of preference, and falls back to "Sprites/Default" (which is always
    /// present in every Unity project, including this one's ray rendering) if none
    /// of the pipeline-specific shaders are found. The result is cached after the
    /// first successful lookup so repeated button creation doesn't re-search.
    /// </summary>
    public static class VRShaderUtil
    {
        private static Shader _cachedButtonShader;

        // Tried in order. Lit shaders first (better in-headset readability with
        // lighting), unlit shaders as a safe universal fallback.
        private static readonly string[] CandidateShaderNames =
        {
            "Universal Render Pipeline/Lit",   // URP
            "Standard",                        // Built-in RP
            "HDRP/Lit",                        // HDRP
            "Universal Render Pipeline/Unlit", // URP unlit fallback
            "Unlit/Color",                     // Built-in unlit fallback
            "Sprites/Default",                 // Always present — used by OpticsManager rays too
        };

        /// <summary>
        /// Returns the best available shader for VR button materials.
        /// Never returns null — falls back to "Sprites/Default" as a last resort,
        /// which ships with every Unity project and is already used elsewhere in
        /// this package for ray rendering.
        /// </summary>
        public static Shader GetButtonShader()
        {
            if (_cachedButtonShader != null)
                return _cachedButtonShader;

            foreach (var name in CandidateShaderNames)
            {
                var shader = Shader.Find(name);
                if (shader != null)
                {
                    _cachedButtonShader = shader;
                    return shader;
                }
            }

            // This should be unreachable — Sprites/Default ships with Unity —
            // but guard against a stripped build just in case.
            Debug.LogWarning("[VRShaderUtil] No candidate shader found; " +
                             "VR buttons may render with the default/error material. " +
                             "Assign a buttonPrefab with your own material to avoid this.");
            return null;
        }

        /// <summary>
        /// Convenience: creates a new Material using the resolved button shader.
        /// </summary>
        public static Material CreateButtonMaterial()
        {
            var shader = GetButtonShader();
            return shader != null ? new Material(shader) : null;
        }
    }
}
