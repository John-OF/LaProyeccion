using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Feedback de un botón de menú: escala suave y sonidos al pasar el mouse
    /// (o al seleccionar con teclado/gamepad) y al hacer click. El sonido lo
    /// reproduce <see cref="MenuAudio"/>; el color/glow lo maneja el ColorBlock
    /// del propio Button.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButtonFX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float lerpSpeed = 12f;

        private Vector3 baseScale;
        private Vector3 targetScale;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            baseScale = transform.localScale;
            targetScale = baseScale;
            button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            targetScale = baseScale;
            transform.localScale = baseScale;
        }

        private void Update()
        {
            // unscaledDeltaTime: el menú no depende de Time.timeScale.
            transform.localScale = Vector3.Lerp(
                transform.localScale, targetScale, Time.unscaledDeltaTime * lerpSpeed);
        }

        private void Hover(bool on)
        {
            if (button != null && !button.interactable) return;
            targetScale = on ? baseScale * hoverScale : baseScale;
            if (on) MenuAudio.Instance?.PlayHover();
        }

        public void OnPointerEnter(PointerEventData e) => Hover(true);
        public void OnPointerExit(PointerEventData e) => Hover(false);
        public void OnSelect(BaseEventData e) => Hover(true);
        public void OnDeselect(BaseEventData e) => Hover(false);

        private void OnClick()
        {
            if (button != null && !button.interactable) return;
            MenuAudio.Instance?.PlayClick();
        }
    }
}
