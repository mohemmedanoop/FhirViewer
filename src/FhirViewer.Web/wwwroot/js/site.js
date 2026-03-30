document.addEventListener("DOMContentLoaded", () => {
  const sliders = document.querySelectorAll("[data-resource-slider]");

  sliders.forEach((slider) => {
    const cards = Array.from(slider.querySelectorAll("[data-slider-card]"));
    const previousButton = slider.querySelector("[data-slider-prev]");
    const nextButton = slider.querySelector("[data-slider-next]");
    const counter = slider.querySelector("[data-slider-counter]");

    if (cards.length === 0 || !previousButton || !nextButton || !counter) {
      return;
    }

    let activeIndex = cards.findIndex((card) => card.classList.contains("is-active"));
    if (activeIndex < 0) {
      activeIndex = 0;
    }

    const render = () => {
      cards.forEach((card, index) => {
        const isActive = index === activeIndex;
        card.classList.toggle("is-active", isActive);
        card.setAttribute("aria-hidden", isActive ? "false" : "true");
      });

      counter.textContent = `${activeIndex + 1} / ${cards.length}`;
      previousButton.disabled = activeIndex === 0;
      nextButton.disabled = activeIndex === cards.length - 1;
    };

    previousButton.addEventListener("click", () => {
      if (activeIndex > 0) {
        activeIndex -= 1;
        render();
      }
    });

    nextButton.addEventListener("click", () => {
      if (activeIndex < cards.length - 1) {
        activeIndex += 1;
        render();
      }
    });

    render();
  });
});
