import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { LocalizationAdminManager } from "../components/LocalizationAdminManager";
import type { LocalizationAdminAdapter } from "../types";

const cultures = [
  {
    cultureCode: "en",
    displayName: "English",
    nativeName: "English",
    isEnabled: true,
    isDefault: true,
    sortOrder: 1
  },
  {
    cultureCode: "fr",
    displayName: "French",
    nativeName: "Francais",
    isEnabled: true,
    isDefault: false,
    sortOrder: 2
  },
  {
    cultureCode: "de",
    displayName: "German",
    nativeName: "Deutsch",
    isEnabled: true,
    isDefault: false,
    sortOrder: 3
  }
];

const translations = [
  {
    translationKeyId: "key-1",
    key: "common.auth.accountLoginMethods.addAnotherLoginMethodMessage",
    defaultValue: "Add another login method",
    values: {
      en: "Add another login method",
      fr: "Ajouter une autre methode de connexion",
      de: ""
    },
    entries: {
      en: {
        value: "Add another login method",
        isMachineTranslated: false,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      },
      fr: {
        value: "Ajouter une autre methode de connexion",
        isMachineTranslated: true,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      },
      de: {
        value: "",
        isMachineTranslated: false,
        updatedAtUtc: null
      }
    },
    updatedAtUtc: "2026-07-13T00:00:00Z"
  }
];

const tree = [
  {
    label: "common",
    fullKey: "common",
    isLeaf: false,
    children: [
      {
        label: "auth",
        fullKey: "common.auth",
        isLeaf: false,
        children: [
          {
            label: "accountLoginMethods",
            fullKey: "common.auth.accountLoginMethods",
            isLeaf: true,
            children: []
          }
        ]
      }
    ]
  }
];

const createAdapter = (canGenerateTranslations = true): LocalizationAdminAdapter => ({
  getCultures: vi.fn().mockResolvedValue(cultures),
  upsertCulture: vi.fn(),
  getTranslations: vi.fn().mockResolvedValue({
    items: translations,
    totalCount: 1,
    page: 1,
    pageSize: 10,
    canGenerateTranslations
  }),
  getTranslationTree: vi.fn().mockResolvedValue(tree),
  upsertTranslation: vi.fn().mockImplementation(async (translationKeyId: string, request) => ({
    ...translations[0],
    translationKeyId,
    values: request.values,
    entries: {
      en: {
        value: request.values.en,
        isMachineTranslated: false,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      },
      fr: {
        value: request.values.fr,
        isMachineTranslated: false,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      },
      de: {
        value: request.values.de,
        isMachineTranslated: false,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      }
    }
  })),
  generateTranslation: vi.fn().mockResolvedValue({
    ...translations[0],
    values: {
      ...translations[0].values,
      fr: "Traduction automatique"
    },
    entries: {
      ...translations[0].entries,
      fr: {
        value: "Traduction automatique",
        isMachineTranslated: true,
        updatedAtUtc: "2026-07-13T00:00:00Z"
      }
    }
  })
});

afterEach(() => {
  cleanup();
});

describe("LocalizationAdminManager", () => {
  it("renders translation cards with paging controls instead of a translations table", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    expect(await screen.findByText(translations[0].key)).toBeTruthy();
    expect(screen.getByText("Key")).toBeTruthy();
    expect(screen.getByLabelText("Items")).toBeTruthy();
    expect(screen.queryByRole("table")).toBeNull();
  });

  it("shows prefix and exact tree options for dotted keys", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    const select = await screen.findByLabelText("Browse key tree");
    await waitFor(() => {
      expect(screen.getByRole("option", { name: "common.auth.*" })).toBeTruthy();
      expect(screen.getByRole("option", { name: "common.auth.accountLoginMethods" })).toBeTruthy();
    });

    fireEvent.change(select, { target: { value: "exact:common.auth.accountLoginMethods" } });

    expect((select as HTMLSelectElement).value).toBe("exact:common.auth.accountLoginMethods");
  });

  it("copies modal textarea edits back into the inline draft when expanded editing is saved", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);
    fireEvent.click(screen.getByRole("button", { name: "Expand" }));

    const dialog = screen.getByRole("dialog");
    const modalTextarea = within(dialog).getByDisplayValue("Ajouter une autre methode de connexion");
    fireEvent.change(modalTextarea, { target: { value: "Traduction etendue" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => {
      expect(screen.getByDisplayValue("Traduction etendue")).toBeTruthy();
    });
  });

  it("renders empty missing translations without language placeholders", async () => {
    const adapter = createAdapter();
    const { container } = render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);

    const textareas = Array.from(container.querySelectorAll("textarea"));
    expect(textareas.length).toBeGreaterThan(0);
    expect(textareas.every((textarea) => textarea.getAttribute("placeholder") === "")).toBe(true);
    expect(screen.queryByPlaceholderText("German")).toBeNull();
  });

  it("shows AI source indicators and supports AI generation when available", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);
    expect(screen.getByLabelText("AI-generated French translation")).toBeTruthy();
    expect(screen.queryByText("AI")).toBeNull();
    expect(screen.queryByText("User")).toBeNull();
    expect(screen.queryByRole("button", { name: "Get English translation" })).toBeNull();
    expect(screen.getByRole("button", { name: "Get French translation" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Get German translation" })).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Get French translation" }));
    expect(adapter.generateTranslation).toHaveBeenCalledWith("key-1", { cultureCode: "fr" });

    await waitFor(() => {
      expect(screen.getByDisplayValue("Traduction automatique")).toBeTruthy();
    });
  });

  it("does not show AI generation actions without a generator adapter", async () => {
    const adapter = createAdapter();
    delete adapter.generateTranslation;

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);

    expect(screen.queryByRole("button", { name: "Get French translation" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Get German translation" })).toBeNull();
  });

  it("shows disabled AI generation actions when the backend generator is unavailable", async () => {
    const adapter = createAdapter(false);

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);

    const frenchButton = screen.getByRole("button", { name: "Get French translation" }) as HTMLButtonElement;
    expect(frenchButton.disabled).toBe(true);
    expect(frenchButton.title).toBe("AI translation is not configured");
  });

  it("shows AI source indicators and generation actions in the expanded modal", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);
    fireEvent.click(screen.getByRole("button", { name: "Expand" }));

    const dialog = screen.getByRole("dialog");
    expect(within(dialog).getByLabelText("AI-generated French translation")).toBeTruthy();
    expect(within(dialog).queryByRole("button", { name: "Get English translation" })).toBeNull();
    expect(within(dialog).getByRole("button", { name: "Get French translation" })).toBeTruthy();
    expect(within(dialog).getByRole("button", { name: "Get German translation" })).toBeTruthy();

    fireEvent.click(within(dialog).getByRole("button", { name: "Get French translation" }));
    expect(adapter.generateTranslation).toHaveBeenCalledWith("key-1", { cultureCode: "fr" });

    await waitFor(() => {
      expect(within(dialog).getByDisplayValue("Traduction automatique")).toBeTruthy();
    });
  });

  it("uses secondary expand and primary save buttons for inline translation actions", async () => {
    const adapter = createAdapter();

    render(<LocalizationAdminManager adapter={adapter} />);
    fireEvent.click(screen.getByRole("button", { name: "Translations" }));

    await screen.findByText(translations[0].key);

    expect(screen.getByRole("button", { name: "Expand" }).className).toContain("btn-outline-secondary");
    expect(screen.getByRole("button", { name: "Save" }).className).toContain("btn-primary");
  });
});
