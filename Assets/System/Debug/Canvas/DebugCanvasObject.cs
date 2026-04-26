using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace Services.DebugUtilities.Canvas
{
    public class DebugCanvasObject : MonoBehaviour
    {
        [Header("Setup")]
        public RectTransform container;
        public GameObject messagePrefab;

        [Header("Config")]
        public int maxMessages = 3;
        public float messageLifetime = 3f;

        private readonly Queue<GameObject> activeMessages = new();

        private void Awake()
        {
            CanvasLoggerService.Initialize(this);
        }

        public void ShowMessage(string message)
        {
            if (activeMessages.Count >= maxMessages)
            {
                var oldest = activeMessages.Dequeue();
                Destroy(oldest);
            }

            GameObject msgObj = Instantiate(messagePrefab, container);

            TMP_Text text = msgObj.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = message;
                text.color = Color.black;
                text.enableAutoSizing = true;
                text.fontSizeMax = 28;
                text.fontSizeMin = 14;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            activeMessages.Enqueue(msgObj);
            StartCoroutine(DestroyAfterTime(msgObj, messageLifetime));
        }

        private IEnumerator DestroyAfterTime(GameObject obj, float time)
        {
            yield return new WaitForSeconds(time);

            if (activeMessages.Contains(obj))
            {
                var tempQueue = new Queue<GameObject>();

                while (activeMessages.Count > 0)
                {
                    var item = activeMessages.Dequeue();
                    if (item != obj)
                        tempQueue.Enqueue(item);
                }

                while (tempQueue.Count > 0)
                    activeMessages.Enqueue(tempQueue.Dequeue());

                Destroy(obj);
            }
        }
    }
}