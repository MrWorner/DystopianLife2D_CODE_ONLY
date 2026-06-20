using Sirenix.OdinInspector;
using UnityEngine;


public interface ITalkable : IInteractable
{
    string[] Dialogues { get; }
    void StartDialogue(Pawn talker);
}

public class NPC : InteractableBase, ITalkable
{
    [BoxGroup("NPC Settings"), SerializeField]
    private string _npcName = "Незнакомец";

    [BoxGroup("Dialogue"), SerializeField, TextArea]
    private string[] _dialogues = new string[]
    {
        "Привет!",
        "Как дела?",
        "До встречи!"
    };

    [BoxGroup("Dialogue"), SerializeField]
    private int _currentDialogueIndex = 0;

    [BoxGroup("UI"), SerializeField]
    private Sprite _talkIcon;

    // IInteractable
    public override Vector3 Position => transform.position;
    public override Transform Transform => transform;
    public override bool CanInteract => _currentDialogueIndex < _dialogues.Length;
    public override int InteractionPriority => 8;
    public override Vector3 InteractionPosition => transform.position;
    public override Sprite GetInteractionIcon() => _talkIcon;

    public override string GetInteractionHint()
    {
        return $"Поговорить с {_npcName} [E]";
    }

    public override void Interact(Pawn interactor)
    {
        StartDialogue(interactor);
    }

    // ITalkable
    public string[] Dialogues => _dialogues;

    public void StartDialogue(Pawn talker)
    {
        if (_currentDialogueIndex >= _dialogues.Length)
        {
            //ColoredDebug.CLog(gameObject, "<color=gray>Диалог завершен</color>");
            return;
        }

        string currentLine = _dialogues[_currentDialogueIndex];

        //ColoredDebug.CLog(gameObject, $"<color=cyan>{_npcName}: {currentLine}</color>");

        // Показать UI диалога
        // DialogueUI.Instance?.ShowDialogue(_npcName, currentLine);

        _currentDialogueIndex++;

        if (_currentDialogueIndex >= _dialogues.Length)
        {
            //ColoredDebug.CLog(gameObject, "<color=lime>Диалог завершен!</color>");
        }
    }

    [Button("Reset Dialogue"), BoxGroup("DEBUG")]
    private void ResetDialogue()
    {
        _currentDialogueIndex = 0;
    }
}