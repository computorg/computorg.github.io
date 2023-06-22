---
layout: post
title: What is expected exactly in terms of reproducibility? And for long-running code?
date: 2023-04-24 00:00:00
tags: reproducibility
description: Discuss the different kinds of reproducibility at play in Computo, and what is expected from the authors.
---

## What kind of reproducibility

Computo is not just about publishing a notebook and proving that it can be compiled with CI! This part of the process is what we call _"Editorial Reproducibility"_. _"Scientific"_ or _"numerical"_ reproducibility of the analyses is also mandatory, on top of classical peer-review evaluation. 

We don't ask people reproducing their data... yet! We also don't ask for bit-wise reproducibility but at least statistical reproducibility.

## Long-running code

If your analyses, model tuning or training phase take a prohibitively long time to compile and integrate, you can include the results of the trained methods in the form of a binary file. However, you must provide the code enabling the user to fully reproduce the training phase, and illustrate your code in a small, toy-sized example.
