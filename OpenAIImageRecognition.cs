using System;
using System.Collections;
using System.Text; // Required for Encoding
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/********************************************************************************************
how to install Newtonsoft.json
Go to Window -> Package Manager.
Click the + button in the top-left corner.
Select "Add package from git URL...".
Enter com.unity.nuget.newtonsoft-json and click "Add". (Requires Unity 2020.1+ and Git installed).
/********************************************************************************************/

// --- C# Classes for OpenAI Request Payload ---
[System.Serializable]
public class OpenAIRequest
{
    public string model;
    public Message[] messages;
    public int max_tokens;
}

[System.Serializable]
public class Message
{
    public string role;
    public ContentPart[] content;
}

[System.Serializable]
public class ContentPart
{
    public string type;
    // Use 'System.NonSerialized' or conditional logic if JsonUtility struggles
    // with having both 'text' and 'image_url' possibly null.
    // Alternatively, use a more robust JSON library like Newtonsoft.Json.
    public string text; // Only used when type="text"
    public ImageUrl image_url; // Only used when type="image_url"
}



[System.Serializable]
public class ImageUrl
{
    public string url;
}

// --- C# Classes for OpenAI Response Payload ---
[System.Serializable]
public class OpenAIResponse
{
    public Choice[] choices;
    public OpenAIError error; // Added for better error reporting
}

[System.Serializable]
public class Choice
{
    public ChoiceMessage message;
    public string finish_reason;
}

[System.Serializable]
public class ChoiceMessage
{
    public string role;
    public string content;
}

// Added for parsing potential error messages from OpenAI
[System.Serializable]
public class OpenAIError
{
    public string message;
    public string type;
    public string param;
    public string code;
}


// --- Main MonoBehaviour Script ---
public class OpenAIImageRecognition : MonoBehaviour
{
    // !!! IMPORTANT: Never hardcode API keys in production code. !!!
    // Use secure methods like environment variables, configuration files outside version control,
    // or dedicated secrets management services. For testing ONLY.
    [SerializeField] private string apiKey = "YOUR_SECURE_API_KEY";

    [Tooltip("The image to be analyzed by OpenAI.")]
    public Texture2D imageToRecogize;

    [Tooltip("The prompt to guide the vision model.")]
    [TextArea(3, 5)] // Makes it easier to edit in Inspector
    public string prompt = "Describe this image in detail.";

    [Tooltip("Maximum number of tokens for the response.")]
    public int maxTokens = 300;

    void Start()
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_SECURE_API_KEY")
        {
            Debug.LogError("OpenAI API Key is not set! Please provide your key in the Inspector or use a secure method to load it.");
            return; // Stop execution if the key is missing
        }
        if (imageToRecogize == null)
        {
            Debug.LogError("Image to recognize (Texture2D) is not assigned in the Inspector.");
            return; // Stop execution if the image is missing
        }
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Debug.LogWarning("Prompt is empty. Using a default prompt.");
            prompt = "What's in this image?";
        }

        // Start the process
        StartCoroutine(SendImageToOpenAI());
    }

    IEnumerator SendImageToOpenAI()
    {
        Debug.Log("Starting image analysis process...");

        // 1. Convert image to Base64 using your TextureConverter
        string base64Image = TextureConverter.ToBase64(imageToRecogize);

        if (string.IsNullOrEmpty(base64Image))
        {
            Debug.LogError("Failed to convert image to Base64. Check TextureConverter and ensure image has Read/Write Enabled.");
            yield break; // Exit coroutine
        }

        // 2. Construct the request payload object
        OpenAIRequest requestPayload = new OpenAIRequest
        {
            model = "gpt-4o", // Use the appropriate vision model
            messages = new Message[]
            {
                new Message
                {
                    role = "user",
                    content = new ContentPart[]
                    {
                        new ContentPart // Text part
                        {
                            type = "text",
                            text = this.prompt // Use the prompt from the Inspector
                        },
                        new ContentPart // Image part
                        {
                            type = "image_url",
                            image_url = new ImageUrl
                            {
                                // Add the data URI prefix. Adjust "jpeg" if your converter uses PNG.
                                url = $"data:image/jpeg;base64,{base64Image}"
                            }
                        }
                    }
                }
            },
            max_tokens = this.maxTokens // Use maxTokens from the Inspector
        };

        // 3. Serialize the payload to JSON and encode to bytes
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore // This is the key change!
        };
        string jsonPayload = JsonConvert.SerializeObject(requestPayload, settings);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);

        // Log the request payload for debugging (optional, can be long)
        // Debug.Log($"Sending JSON Payload: {jsonPayload}");

        // 4. Create and configure the UnityWebRequest
        string url = "https://api.openai.com/v1/chat/completions";
        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            // Set handlers
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();

            // Set headers
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + this.apiKey);

            Debug.Log("Sending request to OpenAI API...");

            // 5. Send the request and wait for response
            yield return www.SendWebRequest();

            // 6. Handle the response

            bool isSuccess = (www.result == UnityWebRequest.Result.Success);

            string responseText = www.downloadHandler?.text ?? "No response body";

            if (isSuccess)
            {
                Debug.Log($"OpenAI Response Received (HTTP {www.responseCode})");
                Debug.Log($"Raw Response: {responseText}");

                try
                {
                    OpenAIResponse response = JsonUtility.FromJson<OpenAIResponse>(responseText);

                    if (response?.choices != null && response.choices.Length > 0)
                    {
                        string responseContent = response.choices[0].message?.content?.Trim();
                        if (!string.IsNullOrEmpty(responseContent))
                        {
                            Debug.Log($"Extracted Content:\n--------------------\n{responseContent}\n--------------------");
                            // <<<<< YOUR LOGIC HERE >>>>>
                            // Process the responseContent (e.g., display it in UI, trigger game events)

                        }
                        else
                        {
                            Debug.LogWarning("OpenAI response content is empty or null.");
                        }
                        Debug.Log($"Finish Reason: {response.choices[0].finish_reason}");
                    }
                    else if (response?.error != null) // Check if OpenAI returned a structured error
                    {
                        Debug.LogError($"OpenAI API Error (parsed from JSON): {response.error.message} (Type: {response.error.type}, Code: {response.error.code})");
                    }
                    else
                    {
                        Debug.LogError("Could not parse OpenAI Response correctly or response was empty. Raw response logged above.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error parsing JSON response: {e.Message}\nRaw Response: {responseText}");
                }
            }
            else
            {
                Debug.LogError($"OpenAI API Request Failed (HTTP {www.responseCode}): {www.error}");
                Debug.LogError($"Error Body: {responseText}"); // Log the body for error details

                // Attempt to parse OpenAI's structured error from the body
                try
                {
                    OpenAIResponse errorResponse = JsonUtility.FromJson<OpenAIResponse>(responseText);
                    if (errorResponse?.error != null)
                    {
                        Debug.LogError($"Parsed Error Details: {errorResponse.error.message} (Type: {errorResponse.error.type}, Code: {errorResponse.error.code})");
                    }
                }
                catch { /* Ignore if parsing fails, primary error already logged */ }
            }
        } // Dispose of UnityWebRequest

        Debug.Log("Image analysis process finished.");

    } // End of SendImageToOpenAI Coroutine
}