#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Toast.Editor
{
    [CustomPropertyDrawer(typeof(ToastControllerCore), true)]
    public class ToastControllerCoreDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = UIElementUtils.CreateBox("トースト通知設定");

            root.Add(new PropertyField(property.FindPropertyRelative("_spacingY")));
            root.Add(new PropertyField(property.FindPropertyRelative("_animDuration")));
            root.Add(new PropertyField(property.FindPropertyRelative("_displayDuration")));
            root.Add(new PropertyField(property.FindPropertyRelative("_firstExtraDisplayDuration")));
            root.Add(new PropertyField(property.FindPropertyRelative("_consecutiveWaitDuration")));

            return root;
        }
    }
}