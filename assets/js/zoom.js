<<<<<<< HEAD
// Initialize medium zoom.
$(document).ready(function() {
  medium_zoom = mediumZoom('[data-zoomable]', {
    margin: 100,
    background: getComputedStyle(document.documentElement)
        .getPropertyValue('--global-bg-color') + 'ee',  // + 'ee' for trasparency.
  })
=======
$(document).ready(function() {
    mediumZoom('[data-zoomable]', {
        margin: 100,
        background: getComputedStyle(document.documentElement)
            .getPropertyValue('--global-bg-color') + 'ee',
    })
>>>>>>> added diagram and zoom
});
