---
layout: page
permalink: /publications/
title: Articles
description: Publications by years in reversed chronological order
order: 2
years: [2021, 1935]
nav: true
---

<div class="publications">

{% for y in page.years %}
  <h2 class="year">{{y}}</h2>
  {% bibliography -f papers -q @*[year={{y}}]* %}
{% endfor %}

</div>
