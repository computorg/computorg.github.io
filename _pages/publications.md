---
layout: page
permalink: /publications/
title: Articles
description: Publications by years in reversed chronological order
order: 2
years: [2021, 1935]
nav: true
---

## Example: a mock contribution

This  page is  a reworking  of the  original t-SNE  article using  the
Computo template. It aims to help authors submitting to the journal by
using some advanced formatting features.

<div class="publications">

{% bibliography --file mock_papers %}

</div>

## Under review

<div class="publications">

{% bibliography --file under_review %}

</div>

## Current Issue

<div class="publications">

{% bibliography --file published --query @*[volume=1,number=0] %}

</div>

## Past Volumes

* [Volume x (year)](#volume-x-year):
* [Volume y (year)](#volume-y-year):

### Volume x (year)

<div class="publications">

{% bibliography --file published --query @*[volume=x,number=3] %}

</div>

### Volume y (year)

<div class="publications">

{% bibliography --file published --query @*[volume=x,number=3] %}

</div>
