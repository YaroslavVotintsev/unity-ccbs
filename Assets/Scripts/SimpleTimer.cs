using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleTimer : MonoBehaviour
{
    public TextMeshProUGUI timerText;

    [Header("Scene View Display")]
    public bool showTimerInSceneView = true;
    public Vector3 sceneViewOffset = new Vector3(0, 2f, 0);

    private float elapsedTime;

    void Update()
    {
        elapsedTime += Time.deltaTime;

        string formattedTime = GetFormattedTime();

        if (timerText != null)
        {
            timerText.text = formattedTime;
        }
    }

    string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        int milliseconds = Mathf.FloorToInt((elapsedTime * 1000f) % 1000f);

        return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showTimerInSceneView) return;
        Handles.color = Color.black;
        Handles.Label(
            transform.position + sceneViewOffset,
            $"Timer: {GetFormattedTime()}"
        );
    }
#endif
}