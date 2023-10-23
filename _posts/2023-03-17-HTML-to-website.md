---
layout: post
title: How to automatically publish the HTML of my contribution to a website?
date: 2023-03-17 00:00:00
tags: reproducibility
description: Describe how to render your article, activate your gh-page and publish your contribution online
---


Some authors reported that their contribution was not published automatically, even when using one of our [3 template repositories](/repos) and even when the build action was successful. This is basically due to the fact that the `gh-pages` is not preporly setup.

We review here the full process for more clarity.

## 1. Check that the build action is correctly configured

If you used one of our template repository, the build action (in `.github/workflows/build.yml`) should look like this:

{% highlight yaml linenos %}
name: build

on:
  workflow_dispatch:
  push:
    branches: main

jobs:
  build-deploy:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - name: Check out repository
        uses: actions/checkout@v2

    [...]

      - name: Render and Publish
        uses: quarto-dev/quarto-actions/publish@v2
        with:
          target: gh-pages
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

{% endhighlight %}

The last step named `Render and Publish` first compiles your notebook and then pushes the HTML and PDF output to a special branch named `gh-pages` which is preferably used by Github to define a web page associated with the current repository, with the address https://user.github.io/repo_name : this is where your final rendered paper should go. If the build action is successful, you don't have to worry and you can move on to the next check.

## 2. Check that gh-pages is activated on your repos

By default, the mechanism that checks if a web page should be published in association with your repository is not activated. You need to go to Settings > page and apply the following configuration:

<div class="row mt-3">
    <div class="col-sm mt-3 mt-md-0">
        {% include figure.html path="assets/img/gh-pages-config.png" class="img-fluid rounded z-depth-1" zoomable=true %}
    </div>
</div>

Once this is done, you may need to trigger the build action for the first successful deployment of your web page.

## 3. One last thing

Don't forget to include the address of the page where your contribution is published to help the reviewer in the `About` section of your repository. For example, for the Computo `R` template, we get :

<div class="row mt-3">
    <div class="col-sm mt-3 mt-md-0">
        {% include figure.html path="assets/img/about-gh-repos-webpage.png" class="img-fluid rounded z-depth-1" zoomable=true %}
    </div>
</div>
