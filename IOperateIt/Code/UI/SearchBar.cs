using AlgernonCommons.UI;
using ColossalFramework;
using ColossalFramework.UI;
using System;
using UnityEngine;

namespace IOperateIt.UI;

public class SearchBar : UIPanel
{
    public float OpenTime { get; set; } = 0.4f;

    private float textFieldWidth = -1f;

    private bool isOpen;

    private string cachedSearchText = string.Empty;

    private UIButton button;

    private UITextField textField;

    public Action<string> OnSearchStarted;
    public Action OnClearResults;
    public Action<float> OnAnimating;

    public static SearchBar Add(UIComponent parent, float xPos, float yPos, string tooltip = null, float width = 500f, float height = 40f)
    {
        SearchBar searchBar = parent.AddUIComponent<SearchBar>();
        searchBar.name = "SearchBar";
        searchBar.relativePosition = new Vector2(xPos, yPos);
        searchBar.size = new Vector2(width, height);
        searchBar.autoLayout = false;
        searchBar.Setup(tooltip);
        return searchBar;
    }
    private void Setup(string tooltip)
    {
        button = AddUIComponent<UIButton>();
        DriveButtons.SetButtonProperties(button);
        button.relativePosition = Vector3.zero;
        button.name = name + nameof(button);
        button.tooltip = tooltip;
        button.size = new Vector2(height, height);
        button.pressedBgSprite = "OptionBasePressed";
        button.normalBgSprite = "OptionBase";
        button.hoveredBgSprite = "OptionBaseHovered";
        button.disabledBgSprite = "OptionBaseDisabled";
        button.normalFgSprite = "AssetSearchIcon";
        button.playAudioEvents = true;
        button.eventKeyDown += CheckKeyboardInput;
        button.eventClick += OnSearchIconClick;

        var textFieldPanel = AttachUIComponent(UITemplateManager.GetAsGameObject("OptionsTextfieldTemplate")) as UIPanel;
        GameObject.Destroy(textFieldPanel.Find<UILabel>("Label"));
        textFieldPanel.absolutePosition = absolutePosition + (Vector3)UILayout.PositionRightOf(button);
        textFieldPanel.size = Vector3.one;

        textField = textFieldPanel.Find<UITextField>("Text Field");
        textField.name = name + nameof(textField);
        textField.tooltip = tooltip;
        textField.relativePosition = Vector3.zero;
        textField.scaleFactor = 0.9f;
        textField.text = string.Empty;
        textField.size = new Vector2(width - button.width, height) * textField.scaleFactor;
        textFieldWidth = textField.width;
        textField.eventGotFocus += OnTextFieldFocused;
        textField.eventLostFocus += OnTextFieldLostFocus;
        textField.eventTextChanged += OnTextChanged;
        textField.eventKeyDown += CheckKeyboardInput;
        textField.isVisible = false;

        size = button.size;
    }

    private void CheckKeyboardInput(UIComponent component, UIKeyEventParameter p)
    {
        if (p.used)
        {
            return;
        }

        if (p.keycode == KeyCode.Escape)
        {
            if (textField.hasFocus && !string.IsNullOrEmpty(textField.text))
            {
                button.Focus();
                p.Use();
            }
            else if (isOpen)
            {
                CloseSearchBar(OpenTime);
                p.Use();
            }
        }
        else if (isOpen)
        {
            if (ValueAnimator.IsAnimating("SearchBarOpen") && !textField.hasFocus)
            {
                textField.Focus();
            }
        }
    }

    private void OnTextFieldLostFocus(UIComponent component, UIFocusEventParameter eventParam)
    {
        textField.text = cachedSearchText;
    }

    private void OnTextChanged(UIComponent component, string value)
    {
        cachedSearchText = value;
        if (value.Length > 1)
        {
            StartSearch();
        }
        else
        {
            ClearResults();
        }
    }

    private void OnTextFieldFocused(UIComponent component, UIFocusEventParameter eventParam)
    {

    }

    public void OnSearchIconClick(UIComponent component, UIMouseEventParameter p)
    {
        if (isOpen)
        {
            CloseSearchBar(OpenTime);
        }
        else
        {
            OpenSearchBar(OpenTime);
        }
    }

    private void StartSearch()
    {
        ClearResults();
        OnSearchStarted?.Invoke(cachedSearchText);
    }
    private void ClearResults()
    {
        OnClearResults?.Invoke();
    }
    private void OpenSearchBar(float time)
    {
        if (ValueAnimator.IsAnimating("SearchBarClose") || ValueAnimator.IsAnimating("SearchBarOpen"))
        {
            return;
        }

        isOpen = true;
        textField.width = 0f;
        textField.isVisible = true;
        textField.text = cachedSearchText;
        textField.SelectAll();
        button.Focus();

        ValueAnimator.Animate("SearchBarOpen", Animating, new AnimatedFloat(0f, 1f, time), OnSearchBarOpened);
    }
    private void Animating(float value)
    {
        textField.width = textFieldWidth * value;
        OnAnimating?.Invoke(value);
    }
    private void OnSearchBarOpened()
    {
        textField.width = textFieldWidth;
        textField.Focus();
        StartSearch();
    }

    public void CloseSearchBar(float time)
    {
        if (ValueAnimator.IsAnimating("SearchBarClose") || ValueAnimator.IsAnimating("SearchBarOpen"))
        {
            return;
        }

        isOpen = false;
        ValueAnimator.Animate("SearchBarClose", Animating, new AnimatedFloat(1f, 0f, time), OnSearchBarClosed);
    }

    private void OnSearchBarClosed()
    {
        textField.width = 0f;
        textField.isVisible = false;
        ClearResults();
    }
}