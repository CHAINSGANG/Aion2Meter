using System;
using System.Collections.Generic;
using System.Linq;

namespace AionFlex.Core.Hotkeys;

public static class HotkeyPolicy
{
	private const int VkF4 = 115;

	public static IReadOnlyDictionary<HotkeyAction, HotkeyBinding> Defaults { get; } = new Dictionary<HotkeyAction, HotkeyBinding>
	{
		[HotkeyAction.ResetDps] = new HotkeyBinding(Enabled: true, HotkeyModifiers.Alt | HotkeyModifiers.Control, 68),
		[HotkeyAction.CopyDps] = new HotkeyBinding(Enabled: true, HotkeyModifiers.Alt | HotkeyModifiers.Control, 67),
		[HotkeyAction.OpenSettings] = new HotkeyBinding(Enabled: true, HotkeyModifiers.Alt, 83),
		[HotkeyAction.ArmTraining] = new HotkeyBinding(Enabled: true, HotkeyModifiers.Alt | HotkeyModifiers.Control, 84)
	};

	public static string Label(HotkeyAction action)
	{
		return action switch
		{
			HotkeyAction.ResetDps => "Reset DPS", 
			HotkeyAction.CopyDps => "Copy DPS", 
			HotkeyAction.OpenSettings => "Open Settings", 
			HotkeyAction.ArmTraining => "Arm training mode", 
			_ => action.ToString(), 
		};
	}

	public static string Format(HotkeyModifiers modifiers, int key)
	{
		List<string> list = new List<string>(5);
		if (modifiers.HasFlag(HotkeyModifiers.Control))
		{
			list.Add("Ctrl");
		}
		if (modifiers.HasFlag(HotkeyModifiers.Alt))
		{
			list.Add("Alt");
		}
		if (modifiers.HasFlag(HotkeyModifiers.Shift))
		{
			list.Add("Shift");
		}
		if (modifiers.HasFlag(HotkeyModifiers.Win))
		{
			list.Add("Win");
		}
		list.Add(KeyName(key));
		return string.Join("+", list);
	}

	public static string Format(HotkeyBinding binding)
	{
		return Format(binding.Modifiers, binding.Key);
	}

	public static string? Validate(HotkeyAction action, HotkeyModifiers modifiers, int key, IReadOnlyDictionary<HotkeyAction, HotkeyBinding> current)
	{
		if ((modifiers & (HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Win)) == 0)
		{
			return "Add Ctrl, Alt or Win to the shortcut.";
		}
		if (!IsAllowedKey(key))
		{
			return "Use a letter, a number or an F-key.";
		}
		if (key == 115 && (modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.Alt)
		{
			return "Alt+F4 closes windows; pick another shortcut.";
		}
		foreach (var (hotkeyAction2, hotkeyBinding2) in current)
		{
			if (hotkeyAction2 != action && hotkeyBinding2.Enabled && hotkeyBinding2.Modifiers == modifiers && hotkeyBinding2.Key == key)
			{
				return "Already used by " + Label(hotkeyAction2) + ".";
			}
		}
		return null;
	}

	public static IReadOnlyDictionary<HotkeyAction, HotkeyBinding> Resolve(IReadOnlyDictionary<string, HotkeySetting>? saved)
	{
		Dictionary<HotkeyAction, HotkeyBinding> dictionary = new Dictionary<HotkeyAction, HotkeyBinding>();
		HotkeyAction[] values = Enum.GetValues<HotkeyAction>();
		for (int i = 0; i < values.Length; i++)
		{
			HotkeyAction hotkeyAction = values[i];
			HotkeyBinding binding = Defaults[hotkeyAction];
			if (saved != null && saved.TryGetValue(hotkeyAction.ToString(), out HotkeySetting value) && value != null)
			{
				HotkeyModifiers modifiers = (HotkeyModifiers)value.Modifiers;
				if ((modifiers & ~(HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Win)) == 0 && Validate(hotkeyAction, modifiers, value.Key, new Dictionary<HotkeyAction, HotkeyBinding>()) == null)
				{
					binding = new HotkeyBinding(value.Enabled, modifiers, value.Key);
				}
			}
			if (binding.Enabled && dictionary.Values.Any((HotkeyBinding b) => b.Enabled && b.Modifiers == binding.Modifiers && b.Key == binding.Key))
			{
				binding = binding with
				{
					Enabled = false
				};
			}
			dictionary[hotkeyAction] = binding;
		}
		return dictionary;
	}

	public static bool IsAllowedKey(int key)
	{
		switch (key)
		{
		case 48:
		case 49:
		case 50:
		case 51:
		case 52:
		case 53:
		case 54:
		case 55:
		case 56:
		case 57:
		case 65:
		case 66:
		case 67:
		case 68:
		case 69:
		case 70:
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 79:
		case 80:
		case 81:
		case 82:
		case 83:
		case 84:
		case 85:
		case 86:
		case 87:
		case 88:
		case 89:
		case 90:
		case 96:
		case 97:
		case 98:
		case 99:
		case 100:
		case 101:
		case 102:
		case 103:
		case 104:
		case 105:
		case 112:
		case 113:
		case 114:
		case 115:
		case 116:
		case 117:
		case 118:
		case 119:
		case 120:
		case 121:
		case 122:
		case 123:
		case 124:
		case 125:
		case 126:
		case 127:
		case 128:
		case 129:
		case 130:
		case 131:
		case 132:
		case 133:
		case 134:
		case 135:
			return true;
		default:
			return false;
		}
	}

	private static string KeyName(int key)
	{
		switch (key)
		{
		case 48:
		case 49:
		case 50:
		case 51:
		case 52:
		case 53:
		case 54:
		case 55:
		case 56:
		case 57:
		case 65:
		case 66:
		case 67:
		case 68:
		case 69:
		case 70:
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 79:
		case 80:
		case 81:
		case 82:
		case 83:
		case 84:
		case 85:
		case 86:
		case 87:
		case 88:
		case 89:
		case 90:
			return ((char)key).ToString();
		case 96:
		case 97:
		case 98:
		case 99:
		case 100:
		case 101:
		case 102:
		case 103:
		case 104:
		case 105:
			return $"Num {key - 96}";
		case 112:
		case 113:
		case 114:
		case 115:
		case 116:
		case 117:
		case 118:
		case 119:
		case 120:
		case 121:
		case 122:
		case 123:
		case 124:
		case 125:
		case 126:
		case 127:
		case 128:
		case 129:
		case 130:
		case 131:
		case 132:
		case 133:
		case 134:
		case 135:
			return $"F{key - 111}";
		default:
			return $"Key {key}";
		}
	}
}
