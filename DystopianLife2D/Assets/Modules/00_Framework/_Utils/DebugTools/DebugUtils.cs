using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DebugUtils
{
    public static void LogMissingReference(MonoBehaviour context, string variableName)
    {
        string className = context.GetType().Name;
        Debug.LogError($"<color=red>ERROR! {className}: {variableName} не задан!</color>", context);
    }

    public static void LogInstanceAlreadyExists(MonoBehaviour context, MonoBehaviour secondContext = null)
    {
        if (context == secondContext)
        {
            return;
        }
        string className = context.GetType().Name;
        Debug.LogError($"<color=red>ERROR! {className}: ActiveInstance уже задан!</color>", context);
        Debug.LogError($"<color=red>ERROR! {className}: ActiveInstance вот он!</color>", secondContext);
    }

    public static void ShowMessage(string text)
    {
        DebugMessageSystemUI.Log(text);
    }

}