<<<<<<< HEAD
<<<<<<< HEAD
// Initialize medium zoom.
$(document).ready(function() {
  medium_zoom = mediumZoom('[data-zoomable]', {
    margin: 100,
    background: getComputedStyle(document.documentElement)
        .getPropertyValue('--global-bg-color') + 'ee',  // + 'ee' for trasparency.
  })
=======
=======
>>>>>>> 0b26c59e6e56e55b846fec8da323ee8348926793
$(document).ready(function() {
    mediumZoom('[data-zoomable]', {
        margin: 100,
        background: getComputedStyle(document.documentElement)
            .getPropertyValue('--global-bg-color') + 'ee',
    })
<<<<<<< HEAD
>>>>>>> added diagram and zoom
=======
>>>>>>> 0b26c59e6e56e55b846fec8da323ee8348926793
});
