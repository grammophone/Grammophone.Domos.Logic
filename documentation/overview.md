# Overview

`Grammophone.Domos.Logic` is the secured business-logic layer for Domos applications.

The library builds on Domos domain containers and access checking. It coordinates the current user, managers, workflows, files, funds transfer processing, channels and change logging.

Logic code is generic over the user and domain-container types. The domain container should be a provider-neutral Domos contract, normally supplied by an EF6 or EF Core adapter.

Query code uses `Grammophone.DataAccess.QueryExtensions`, making logic independent of EF6 and EF Core query-extension namespaces.
