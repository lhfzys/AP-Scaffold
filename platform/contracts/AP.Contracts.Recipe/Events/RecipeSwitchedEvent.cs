using MediatR;

namespace AP.Contracts.Recipe.Events;

/// <summary>配方切换事件：切换成功后发布，业务插件可订阅做联动。</summary>
public sealed record RecipeSwitchedEvent(
    string Code,
    string Name,
    int Version,
    string? PreviousCode,
    DateTime SwitchedAt) : INotification;
