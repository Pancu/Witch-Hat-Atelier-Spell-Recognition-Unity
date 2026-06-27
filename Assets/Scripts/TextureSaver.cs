using UnityEngine;
using System.IO;

// Use this utility class to save a Texture2D as a PNG file in the "DrawnSymbols" folder within the Assets directory.
public static class TextureSaver
{
    public static void SaveTextureAsPNG(Texture2D texture, string fileName)
    {
        string folderPath = Path.Combine(Application.dataPath, "DrawnSymbols");
        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        // Folder path
        string filePath = Path.Combine(folderPath, fileName + ".png");

        // Convert the texture to PNG format
        byte[] pngData = texture.EncodeToPNG();

        // Save
        File.WriteAllBytes(filePath, pngData);

        Debug.Log($"Saved texture in: {filePath}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}
