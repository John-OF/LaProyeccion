using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LaProyeccion.UI
{
    /// <summary>
    /// Feedback de selección para elementos de UI que no son botones
    /// (sliders, toggle, dropdown): escala suave + sonido al pasar el mouse o al
    /// seleccionar con teclado/gamepad. Da el "estás aquí" que faltaba al navegar
    /// las opciones con el mando. El resaltado de color lo aporta el ColorBlock
    /// del propio Selectable.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class MenuSelectableFX : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float lerpSpeed = 12f;

        private Vector3 baseScale;
        private Vector3 targetScale;
        private Selectable selectable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
            targetScale = baseScale;
        }

        private void OnEnable()
        {
            targetScale = baseScale;
            transform.localScale = baseScale;
        }

        private void Update()
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale, targetScale, Time.unscaledDeltaTime * lerpSpeed);
        }

        private void Hover(bool on)
        {
            if (selectable != null && !selectable.interactable) return;
            targetScale = on ? baseScale * hoverScale : baseScale;
            if (on) MenuAudio.Instance?.PlayHover();
        }

        public void OnPointerEnter(PointerEventData e) => Hover(true);
        public void OnPointerExit(PointerEventData e) => Hover(false);
        public void OnSelect(BaseEventData e) => Hover(true);
        public void OnDeselect(BaseEventData e) => Hover(false);
    }
}
