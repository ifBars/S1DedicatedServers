(function () {
  const header = document.querySelector('.site-header');
  if (!header) {
    return;
  }

  const updateHeaderElevation = () => {
    if (window.scrollY > 12) {
      header.style.boxShadow = '0 8px 28px rgba(0, 0, 0, 0.24)';
    } else {
      header.style.boxShadow = 'none';
    }
  };

  updateHeaderElevation();
  window.addEventListener('scroll', updateHeaderElevation, { passive: true });
})();
