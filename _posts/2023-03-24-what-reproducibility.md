---
layout: post
title: What is expected exactly in terms of reproducibility?
date: 2023-07-04 00:00:00
tags: reproducibility
description: Discuss the different kinds of reproducibility at play in Computo, and what is expected from the authors.
---

Computo is not just about publishing a notebook and proving that it can be compiled with CI! This part of the process is what we call _"Editorial Reproducibility"_. _"Scientific"_ or _"numerical"_ reproducibility of the analyses is also mandatory, on top of classical peer-review evaluation. 

We don't ask people reproducing their data... yet! We also don't ask for "bit-wise computational" reproducibility (i.e. obtaining exactly the same results bit-by-bit) but rather a "statistical" reproducibility, i.e. obtaining results leading to the same conclusion, with potential non-significant statistical variability.

![Reproducible Workflow](img/reproducible-sequence.svg)

Indeed, the global scientific workflow of a reproducible process for a Computo may be split in two types of steps:

External
: This part of the process may be conducted outside of the notebook environment, for a list of reasons (non-exclusive to each other):

- the process is too long to be conducted in a notebook
- the data to be processed is too big to be handled directly in the notebook
- it needs a specific environment (e.g. a cluster, with gpus, etc.)
- it needs to involve specific languages (e.g. C, C++, Fortran, etc.) or build tools (e.g. make, cmake, etc.)

It is “Computational reproducibility”, where the reproducibility is achieved by providing the code and the environment to run it, but not the results themselves.

Editorial
: This is where the notebook presents the results of the external process, and where everything is put together to produce the final document, it is “Direct reproducibility” in the sense that the notebook is the only thing needed to reproduce the results.

Ultimately, the workflow must end with a direct reproducibility step which concludes the whole process. 

When applicable, the switch from external to editorial reproducibility is done with a “data transfer” step, where the data produced by the external process is transferred to the notebook environment. It’s required that not only the intermediate results are provided, but also the code to transfer it in the notebook environment. They are a variety of software solutions to do so.

## Examples of data transfer solutions

### Intermediate results storage
- in python environment: the [`joblib.Memory`](https://joblib.readthedocs.io/en/latest/memory.html) class which provides a caching mechanism for python functions, and can be used to save the results of a function call to disk, and load it back later.
- in R environment: the `.RData` file format, which can be loaded back in R with the `load()` function.

### Transfer of the results to the notebook environment
- for both aforementioned solutions, the results (`.joblib` directory or `.Rdata` file) could be committed to the git repository, and directly loaded in the notebook environment.
- Another solution is to centralize input data (when large enough) and intermediate results on a shared scientific provider (we recommend [Zenodo](https://zenodo.org/) for this purpose), and download them in the notebook environment.
