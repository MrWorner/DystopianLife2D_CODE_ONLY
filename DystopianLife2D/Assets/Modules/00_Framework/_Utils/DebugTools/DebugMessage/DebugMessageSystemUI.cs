using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class DebugMessageSystemUI : MonoBehaviour
{
    [BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private GameObject _messageTextPrefab;
    [BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private RectTransform _messagesPanel;

    [SerializeField] private int _maxMessages = 5;
    [SerializeField] private float _defaultMessageLifetime = 3f;
    private float chatLifetime = 0f;

    private List<MessageItem> _activeMessages = new List<MessageItem>();
    private static DebugMessageSystemUI _instance;
    private Coroutine _chatLifetimeCoroutine;

    [Serializable]
    private class MessageItem
    {
        public GameObject Panel;
        public TextMeshProUGUI TextComponent;
        public float Lifetime;
        public Coroutine LifetimeCoroutine;
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        if (_messageTextPrefab == null) DebugUtils.LogMissingReference(this, nameof(_messageTextPrefab));
        if (_messagesPanel == null) DebugUtils.LogMissingReference(this, nameof(_messagesPanel));
    }

    public static void Log(string message, float lifetime = 0)
    {
        if (_instance == null)
        {
            Debug.LogWarning("DebugMessageSystem не найден в сцене!");
            return;
        }

        _instance.StartCoroutine(_instance.AddMessageNextFrame(message, lifetime));
    }

    private IEnumerator AddMessageNextFrame(string message, float lifetime)
    {
        yield return null;
        AddMessageInternal(message, lifetime);
    }


    private void AddMessageInternal(string message, float lifetime)
    {
        if (_messagesPanel == null || _messageTextPrefab == null)
        {
            Debug.LogError("Не настроены компоненты DebugMessageSystem!");
            return;
        }

        if (_activeMessages.Count >= _maxMessages)
        {
            RemoveOldestMessage();
        }

        var newPanel = Instantiate(_messageTextPrefab, _messagesPanel);
        TextMeshProUGUI newText = newPanel.GetComponentInChildren<TextMeshProUGUI>();
        if (newText == null)
        {
            Debug.LogError("Не найден TextMeshProUGUI в префабе сообщения!");
            Destroy(newPanel);
            return;
        }
        newText.text = message;

        MessageItem newItem = new MessageItem
        {
            Panel = newPanel,
            TextComponent = newText,
            Lifetime = lifetime <= 0 ? _defaultMessageLifetime : lifetime
        };

        _activeMessages.Insert(0, newItem);
        newItem.LifetimeCoroutine = StartCoroutine(MessageLifetimeRoutine(newItem));

        UpdateMessagesPositions();

        if (chatLifetime > 0)
        {
            if (_chatLifetimeCoroutine != null)
            {
                StopCoroutine(_chatLifetimeCoroutine);
            }
            _chatLifetimeCoroutine = StartCoroutine(ChatLifetimeRoutine());
        }
    }

    private void UpdateMessagesPositions()
    {
        for (int i = 0; i < _activeMessages.Count; i++)
        {
            if (_activeMessages[i].Panel != null)
            {
                RectTransform rectTransform = _activeMessages[i].Panel.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.SetSiblingIndex(i);
                }
            }
        }
    }

    private void RemoveOldestMessage()
    {
        if (_activeMessages.Count > 0)
        {
            int lastIndex = _activeMessages.Count - 1;
            MessageItem oldestItem = _activeMessages[lastIndex];

            if (oldestItem.LifetimeCoroutine != null)
            {
                StopCoroutine(oldestItem.LifetimeCoroutine);
            }

            if (oldestItem.Panel != null)
            {
                Destroy(oldestItem.Panel);
            }

            _activeMessages.RemoveAt(lastIndex);
        }
    }

    private IEnumerator MessageLifetimeRoutine(MessageItem item)
    {
        yield return new WaitForSeconds(item.Lifetime);

        if (_activeMessages.Contains(item))
        {
            if (item.Panel != null)
            {
                Destroy(item.Panel);
            }
            _activeMessages.Remove(item);
            UpdateMessagesPositions();
        }
    }

    private IEnumerator ChatLifetimeRoutine()
    {
        yield return new WaitForSeconds(chatLifetime);
        ClearAllMessages();
    }

    private void ClearAllMessages()
    {
        List<MessageItem> itemsToClear = new List<MessageItem>(_activeMessages);
        _activeMessages.Clear();

        foreach (var item in itemsToClear)
        {
            if (item.LifetimeCoroutine != null)
            {
                StopCoroutine(item.LifetimeCoroutine);
            }

            if (item.Panel != null)
            {
                Destroy(item.Panel);
            }
        }
    }
}