---
layout: post
title: 'I use a different language than Python, R or Julia: would Computo accept my contributions?'
date: 2023-03-24 00:00:00
tags: [reproducibility, code]
description: Describe how to handle other languages than R, Julia or Python
---

In principle, we are open to any kind of language.

In practice, we need to integrate reproducible and compilable code into our quarto template. Natively, we support, `R`, `Python` and `Julia` and give dedicated template. For others, if the language is supported by a Jupyter kernel ([there are kernels for many languages](https://gist.github.com/chronitis/682c4e0d9f663e85e3d87e97cd7d1624), [quarto allows code execution](https://quarto.org/docs/computations/execution-options.html#engine-binding).

When writing your contribution though, keep in mind that some languages are not designed for interactivity and that there will be a formatting effort to support your point in your manuscript (which could be as expensive as interfacing this code with Python or R via `pybind11`, `Rcpp` or equivalent). It's your choice.

From our side, we will do our best for the technical aspects to help with the integration of any language, but the editorial board and reviewers will also do the work to make sure the contribution is within the bounds scientifically and in the spirit of reproducibility.
