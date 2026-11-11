# Folium

Folium is a personal library of books that stays on one device.

## Language

**Personal library**:
The books a person saves on one device.
_Avoid_: Catalog, collection, shelf

**Book**:
A saved book with its own local identifier. Two books can share a title and an author. Each book remains distinct.
_Avoid_: Entry, record, item, document

**Title**:
The name of a book. A book requires a title.
_Avoid_: Name

**Author**:
The writer named on a book. A book requires one author.
_Avoid_: Writer, creator

**Reading status**:
The place of a saved book in the person's reading. The values are to read, reading, and finished.
_Avoid_: Progress, state, shelf

**Find online**:
The task that searches Open Library. It needs a network. It is not a search of saved books.

**Provider work**:
An Open Library grouping of editions. A search result is not a saved book.

**Edition**:
A specific publication of a provider work. ISBN, publisher, publication date and
cover belong to that edition.

**Preview**:
A read-only view of provider metadata. A preview does not save a book.

**Import draft**:
An editable copy of a selected edition before the person saves it as a book.
Canceling the draft does not change the personal library.

**Source identifiers**:
The original Open Library work path, edition path, and cover identifier retained
with an imported book. Local metadata edits do not change these identifiers.

**Saved cover**:
A private image copy that the person confirms they have permission to save.
A book does not require a saved cover.

**Personal library backup**:
A file of the saved books from one device, with reading status, metadata, source identifiers, and saved covers.
Version 1 uses the kind `personal-library-backup`.
The file is separate from the private library file.
Folium reads version 1 up to 32 MiB and does not upload the file.
Restore replaces the saved books after confirmation.
_Avoid_: Library file, sync, cloud backup
