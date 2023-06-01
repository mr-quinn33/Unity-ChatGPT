using System.Collections;
#if UNITY_EDITORCOROUTINES
using Unity.EditorCoroutines.Editor;
#endif
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityChatGPT.Editor
{
    public class ChatGPTEditor : EditorWindow
    {
        private const string apiUrl = "https://api.openai.com/v1/chat/completions";

        private string apiKey;
        private string prompt;
        private string generatedMessage;
        private Vector2 scrollPosition;
        private bool isGeneratingMessage;
        private bool showLoadingSpinner;
        private bool showApiKey;

        [MenuItem("Window/ChatGPT Plugin")]
        public static void ShowWindow()
        {
            GetWindow(typeof(ChatGPTEditor)).titleContent.text = "ChatGPT Plugin";
        }

        private void OnGUI()
        {
            GUILayout.Label(nameof(ChatGPTEditor), EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            prompt = EditorGUILayout.TextField("Prompt", prompt);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                prompt = string.Empty;
                GUI.FocusControl("Prompt");
            }

            EditorGUILayout.EndHorizontal();

            showApiKey = GUILayout.Toggle(showApiKey, "Show API Key");

            if (showApiKey)
            {
                apiKey = EditorGUILayout.TextField("API Key", apiKey);

                if (GUILayout.Button("Save"))
                {
                    EditorPrefs.SetString("APIKey", apiKey);
                }

                if (GUILayout.Button("Load"))
                {
                    apiKey = EditorPrefs.GetString("APIKey", string.Empty);
                    GUI.FocusControl("API Key");
                }

                if (GUILayout.Button("Clear"))
                {
                    EditorPrefs.DeleteKey("APIKey");
                    apiKey = string.Empty;
                    GUI.FocusControl("API Key");
                }
            }

            EditorGUI.BeginDisabledGroup(isGeneratingMessage);
            if (GUILayout.Button("Generate Message"))
            {
                isGeneratingMessage = true;
                showLoadingSpinner = true;
#if UNITY_EDITORCOROUTINES
                EditorCoroutineUtility.StartCoroutineOwnerless(HandleGenerateMessage(prompt));
#endif
            }

            EditorGUI.EndDisabledGroup();

            if (showLoadingSpinner)
            {
                EditorGUILayout.LabelField("Generating message...");
                DrawLoadingSpinner();
            }
            else if (!string.IsNullOrEmpty(generatedMessage))
            {
                EditorGUILayout.LabelField("Generated Message:");
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                EditorGUILayout.TextArea(generatedMessage, new GUIStyle(EditorStyles.textArea));
                EditorGUILayout.EndScrollView();
            }
        }

        private IEnumerator HandleGenerateMessage(string prompt)
        {
#if UNITY_EDITORCOROUTINES
            yield return EditorCoroutineUtility.StartCoroutineOwnerless(GenerateMessage(prompt));
#else
            yield return null;
#endif
            isGeneratingMessage = false;
            showLoadingSpinner = false;
        }

        private IEnumerator GenerateMessage(string prompt)
        {
            var payload = new Gpt3Request
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new Message
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            string jsonPayload = JsonUtility.ToJson(payload);
            var request = new UnityWebRequest(apiUrl, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
#if DEBUG
                Debug.LogError("ChatGPT API request failed: " + request.error);
                Debug.LogError("Response body: " + request.downloadHandler.text);
#endif
                generatedMessage = "Error: " + request.error;
            }
            else
            {
                string responseJson = request.downloadHandler.text;
                var responseData = JsonUtility.FromJson<APIResponseData>(responseJson);
                generatedMessage = responseData.choices[0].message.content;
                generatedMessage = generatedMessage.Trim();
            }

            request.Dispose();
        }

        private void DrawLoadingSpinner()
        {
            const float spinnerSize = 24f;
            Rect spinnerRect = GUILayoutUtility.GetRect(spinnerSize, spinnerSize, GUILayout.ExpandWidth(false));
            float angle = -(float) (EditorApplication.timeSinceStartup % 1f) * 360f;
            GUIUtility.RotateAroundPivot(angle, spinnerRect.center);
            GUI.DrawTexture(spinnerRect, EditorGUIUtility.IconContent("RotateTool").image, ScaleMode.StretchToFill);
            GUIUtility.RotateAroundPivot(-angle, spinnerRect.center);
            Repaint();
        }

        [System.Serializable]
        private class APIResponseData
        {
            public ChoiceData[] choices;
        }

        [System.Serializable]
        private class ChoiceData
        {
            public Message message;
        }

        [System.Serializable]
        private class Gpt3Request
        {
            // ReSharper disable once NotAccessedField.Local
            public string model;

            // ReSharper disable once NotAccessedField.Local
            public Message[] messages;
        }

        [System.Serializable]
        private class Message
        {
            // ReSharper disable once NotAccessedField.Local
            public string role;

            // ReSharper disable once NotAccessedField.Local
            public string content;
        }
    }
}