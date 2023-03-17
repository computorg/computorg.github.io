---
layout: page
permalink: /repos/
title: Repositories
nav: true
nav_order: 5
---

Check our pinned repositories for our quarto extension, some templates for authors and an advanced mock contribution.

{% if site.data.repositories.github_repos %}
<div class="repositories d-flex flex-wrap flex-md-row flex-column justify-content-between align-items-center">
  {% for repo in site.data.repositories.github_repos %}
    {% include repository/repo.html repository=repo %}
  {% endfor %}
</div>
{% endif %}
