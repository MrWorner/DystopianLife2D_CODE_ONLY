using System;
using UniRx;
using UnityEngine;

public static class UniRxExtensions
{
    /// <summary>
    /// Логирует события потока через вашу систему ColoredDebug
    /// </summary>
    public static IObservable<T> LogColored<T>(this IObservable<T> source, GameObject context, string name)
    {
        return source.Do(
            onNext: x => ColoredDebug.CLog(context, $"<color=cyan>[UniRx] {name}:</color> OnNext -> {x}", true),
            onError: ex => ColoredDebug.CLog(context, $"<color=red>[UniRx] {name}:</color> ERROR -> {ex.Message}", true),
            onCompleted: () => ColoredDebug.CLog(context, $"<color=grey>[UniRx] {name}:</color> Completed", true)
        );
    }
}