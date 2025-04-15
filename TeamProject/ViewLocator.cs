using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;

namespace TeamProject;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data == null)
            return null;

        var type = data.GetType();
        if (type == typeof(string) || type.IsValueType)
            return new TextBlock { Text = data.ToString() };

        var viewTypeName = type.FullName?.Replace("ViewModel", "View");
        if (viewTypeName == null)
            return null;

        var viewType = Type.GetType(viewTypeName);
        if (viewType != null && Activator.CreateInstance(viewType) is Control view)
            return view;

        return new TextBlock { Text = $"[View Not Found for {type.Name}]" };
    }

    public bool Match(object? data)
    {
        return data is not null;
    }
}
