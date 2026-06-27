using System.Collections.Generic;
using UnityEngine;

public class SpellVFXMaker : MonoBehaviour
{
    [Header("VFX Prefabs")]
    [SerializeField] private GameObject fireCorePrefab;
    [SerializeField] private GameObject columnPrefab;
    // Add other VFX prefabs here as needed

    public void CreateSpellEffects(List<AnalyzedSymbolData> symbols, float outerCircleRadius, Vector3 circleCenter3D)
    {
        Debug.LogWarning($"[VFX Maker]: Compiling spell. Circle radius: {outerCircleRadius}. Internal symbols: {symbols.Count}");

        string primaryElement = "";
        float elementAccuracy = 0f;
        float elementSize = 1f;

        List<AnalyzedSymbolData> modifiers = new List<AnalyzedSymbolData>();

        // Divide elements in primary and modifiers
        foreach (var symbol in symbols)
        {
            if (symbol.ClassLabel == "Fire Sigil")
            {
                primaryElement = symbol.ClassLabel;
                elementAccuracy = symbol.Accuracy;
                elementSize = symbol.Size;
            }
            else if (symbol.ClassLabel == "Column")
            {
                modifiers.Add(symbol);
            }
        }

        // If no primary element found, exit
        if (string.IsNullOrEmpty(primaryElement))
        {
            Debug.LogError("[VFX Maker]: No elemental sigil found. Null spell.");
            return;
        }

        // Generate primary element VFX (for now, only Fire is implemented)
        if (primaryElement == "Fire Sigil" && fireCorePrefab != null)
        {
            // Instantiate the effect at the center of the magic circle
            GameObject coreVFX = Instantiate(fireCorePrefab, circleCenter3D, Quaternion.identity);

            // Scale the effect based on the size of the drawn outer circle!
            coreVFX.transform.localScale = Vector3.one * (outerCircleRadius * 2f);

            Debug.Log($"[VFX]: Created fire core scaled to radius: {outerCircleRadius}");
        }

        // Generating modifier effects (for now, only Column is implemented)
        foreach (var mod in modifiers)
        {
            if (mod.ClassLabel == "Column" && columnPrefab != null)
            {
                /*// Applichiamo la rotazione calcolata in C# sull'asse Y
                Quaternion vfxRotation = Quaternion.Euler(0, -mod.RotationAngle, 0);

                // Istanziamo l'effetto nella posizione esatta in cui l'utente ha disegnato il simbolo
                GameObject columnVFX = Instantiate(columnPrefab, mod.CenterPosition, vfxRotation);

                // Modifichiamo la lunghezza o la potenza del VFX in base a quanto grande l'ha disegnato l'utente
                columnVFX.transform.localScale = new Vector3(mod.Size, mod.Size, mod.Size * 2f);

                Debug.Log($"[VFX]: Evocata colonna direzionale a {mod.RotationAngle}° in posizione {mod.CenterPosition}");*/
            }
        }
    }
}
