using System;
using UnityEngine; // Required for Texture2D, RenderTexture, Graphics, etc.

/// <summary>
/// Provides utility methods for converting Unity Texture2D objects.
/// </summary>
public static class TextureConverter
{
    /// <summary>
    /// Converts a Texture2D into a Base64 encoded string using the specified format and quality.
    /// Handles potential non-readability issues by creating a temporary readable copy.
    /// </summary>
    /// <param name="texture">The Texture2D to convert.</param>
    /// <param name="format">The encoding format (JPG or PNG).</param>
    /// <param name="jpegQuality">The quality level for JPG encoding (1-100). Ignored if format is PNG.</param>
    /// <returns>Base64 string representation of the image, or null on failure.</returns>
    public static string ToBase64(Texture2D texture, ImageFormat format = ImageFormat.JPG, int jpegQuality = 85)
    {
        if (texture == null)
        {
            Debug.LogError("ToBase64: Input texture is null.");
            return null;
        }

        // Clamp JPEG quality
        if (format == ImageFormat.JPG)
        {
            jpegQuality = Mathf.Clamp(jpegQuality, 1, 100);
        }

        Texture2D readableTexture = null;
        byte[] imageBytes = null;
        string base64String = null;

        try
        {
            // 1. Ensure the texture is readable by creating a temporary copy
            readableTexture = DuplicateReadableTexture(texture);
            if (readableTexture == null)
            {
                // Error already logged in DuplicateReadableTexture
                return null;
            }

            // 2. Encode the readable texture to bytes based on the chosen format
            switch (format)
            {
                case ImageFormat.JPG:
                    imageBytes = readableTexture.EncodeToJPG(jpegQuality);
                    break;
                case ImageFormat.PNG:
                    imageBytes = readableTexture.EncodeToPNG();
                    break;
                default:
                    Debug.LogError($"ToBase64: Unsupported image format requested: {format}");
                    return null;
            }

            if (imageBytes == null || imageBytes.Length == 0)
            {
                Debug.LogError($"ToBase64: Failed to encode readable texture to {format} bytes.");
                return null;
            }

            // 3. Convert bytes to Base64 string
            base64String = Convert.ToBase64String(imageBytes);
        }
        catch (Exception e)
        {
            Debug.LogError($"ToBase64: Error during conversion: {e.Message}\n{e.StackTrace}");
            return null; // Return null on error
        }
        finally
        {
            // 4. IMPORTANT: Clean up the temporary readable texture copy
            if (readableTexture != null)
            {
                // Use DestroyImmediate if called from editor code outside play mode
                // Use Destroy if called during play mode
                // UnityEngine.Object is the base class for Unity objects like Texture2D
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(readableTexture);
                else
                    UnityEngine.Object.DestroyImmediate(readableTexture);
            }
        }

        return base64String;
    }

    /// <summary>
    /// Defines the target image encoding format.
    /// </summary>
    public enum ImageFormat
    {
        JPG,
        PNG
    }

    /// <summary>
    /// Creates a temporary, readable copy of a Texture2D. Essential if the
    /// source texture might be compressed or marked non-readable in import settings.
    /// The caller is responsible for Destroying the returned texture after use,
    /// although the primary ToBase64 method handles this automatically.
    /// </summary>
    /// <param name="source">The source Texture2D.</param>
    /// <returns>A new, readable Texture2D, or null if an error occurs.</returns>
    private static Texture2D DuplicateReadableTexture(Texture2D source)
    {
        if (source == null)
        {
            Debug.LogError("DuplicateReadableTexture: Source texture is null.");
            return null;
        }

        RenderTexture renderTex = null;
        Texture2D readableText = null;

        try
        {
            // Get a temporary RenderTexture suitable for reading pixels
            renderTex = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0, // No depth buffer
                        RenderTextureFormat.ARGB32, // A common, reliable format
                        RenderTextureReadWrite.Default); // Or Linear depending on Color Space

            // Copy the source texture to the temporary RenderTexture
            Graphics.Blit(source, renderTex);

            // Backup the currently active RenderTexture
            RenderTexture previous = RenderTexture.active;
            // Set the temporary RenderTexture as active to read from it
            RenderTexture.active = renderTex;

            // Create the new readable Texture2D
            // Using RGBA32 format, common for ReadPixels
            // false = no mipmaps needed for this copy
            readableText = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);

            // Read the pixels from the active RenderTexture into the new Texture2D
            readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableText.Apply(); // Commit the pixel changes

            // Restore the previously active RenderTexture
            RenderTexture.active = previous;
        }
        catch (Exception e)
        {
            Debug.LogError($"DuplicateReadableTexture: Error creating readable copy: {e.Message}\n{e.StackTrace}");
            // Clean up potential intermediate objects if error occurred mid-process
            if (readableText != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(readableText); else UnityEngine.Object.DestroyImmediate(readableText);
            }
            readableText = null; // Ensure null is returned on error
        }
        finally
        {
            // IMPORTANT: Always release the temporary RenderTexture
            if (renderTex != null)
            {
                RenderTexture.ReleaseTemporary(renderTex);
            }
        }

        // Return the readable texture (or null if error occurred)
        return readableText;
    }
}