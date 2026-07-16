
import { type FormEvent, useEffect, useMemo, useState } from "react";
import type {
  LocalizationAdminAdapter,
  LocalizationTranslation,
  LocalizationTranslationTreeNode,
  LocalizationTranslationValue,
  SupportedCulture,
  UpsertSupportedCultureRequest
} from "../types";

const emptyCultureForm: UpsertSupportedCultureRequest = {
  cultureCode: "",
  displayName: "",
  nativeName: "",
  isEnabled: true,
  isDefault: false,
  sortOrder: 100
};

type AdminLocalizationTab = "languages" | "translations";
type TreeFilterMode = "prefix" | "exact";

interface TreeFilterOption {
  value: string;
  label: string;
  mode: TreeFilterMode;
  keyPrefix?: string;
  exactKey?: string;
}

interface ExpandedTranslationState {
  translationKeyId: string;
  key: string;
  defaultValue: string;
  values: Record<string, string>;
  entries: Record<string, LocalizationTranslationValue>;
}

interface TranslationGenerationState {
  translationKeyId: string;
  cultureCode: string;
}

export interface LocalizationAdminManagerProps {
  adapter: LocalizationAdminAdapter;
  className?: string;
}

const flattenTreeOptions = (nodes: LocalizationTranslationTreeNode[], depth = 0): TreeFilterOption[] => {
  const indent = depth > 0 ? `${"\u00A0\u00A0".repeat(depth)}` : "";

  return nodes.flatMap((node) => {
    const groupOption = node.children.length > 0
      ? [{
          value: `prefix:${node.fullKey}`,
          label: `${indent}${node.fullKey}.*`,
          mode: "prefix" as const,
          keyPrefix: node.fullKey
        }]
      : [];

    const exactOption = node.isLeaf
      ? [{
          value: `exact:${node.fullKey}`,
          label: `${indent}${node.fullKey}`,
          mode: "exact" as const,
          exactKey: node.fullKey
        }]
      : [];

    return [
      ...groupOption,
      ...exactOption,
      ...flattenTreeOptions(node.children, depth + 1)
    ];
  });
};

const AutoTranslateIcon = () => (
  <svg aria-hidden="true" viewBox="0 0 16 16" width="16" height="16" fill="currentColor">
    <path d="M8.5 1.5a.5.5 0 0 1 .47.329l.794 2.118 2.118.794a.5.5 0 0 1 0 .938l-2.118.794-.794 2.118a.5.5 0 0 1-.938 0l-.794-2.118-2.118-.794a.5.5 0 0 1 0-.938l2.118-.794.794-2.118A.5.5 0 0 1 8.5 1.5Zm4 7a.5.5 0 0 1 .465.316l.414 1.034 1.034.414a.5.5 0 0 1 0 .928l-1.034.414-.414 1.034a.5.5 0 0 1-.928 0l-.414-1.034-1.034-.414a.5.5 0 0 1 0-.928l1.034-.414.414-1.034A.5.5 0 0 1 12.5 8.5ZM3 8a.5.5 0 0 1 .472.334l.31.931.931.31a.5.5 0 0 1 0 .95l-.931.31-.31.931a.5.5 0 0 1-.944 0l-.31-.931-.931-.31a.5.5 0 0 1 0-.95l.931-.31.31-.931A.5.5 0 0 1 3 8Z" />
  </svg>
);

const AiGeneratedIndicatorIcon = () => (
  <svg aria-hidden="true" viewBox="0 0 16 16" width="14" height="14" fill="currentColor">
    <path d="M8 1.5a.5.5 0 0 1 .447.276l1.118 2.236 2.236 1.118a.5.5 0 0 1 0 .894L9.565 7.142 8.447 9.378a.5.5 0 0 1-.894 0L6.435 7.142 4.199 6.024a.5.5 0 0 1 0-.894l2.236-1.118 1.118-2.236A.5.5 0 0 1 8 1.5Zm4.5 7a.5.5 0 0 1 .447.276l.5 1 .999.5a.5.5 0 0 1 0 .894l-.999.5-.5 1a.5.5 0 0 1-.894 0l-.5-1-.999-.5a.5.5 0 0 1 0-.894l.999-.5.5-1A.5.5 0 0 1 12.5 8.5Z" />
  </svg>
);

const normalizeEntries = (translation: LocalizationTranslation): Record<string, LocalizationTranslationValue> => {
  if (translation.entries) {
    return { ...translation.entries };
  }

  return Object.fromEntries(
    Object.entries(translation.values ?? {}).map(([cultureCode, value]) => [
      cultureCode,
      {
        value,
        isMachineTranslated: false,
        updatedAtUtc: null
      }
    ])
  );
};

const normalizeDraftValues = (translation: LocalizationTranslation): Record<string, string> => {
  const entries = normalizeEntries(translation);
  return Object.fromEntries(Object.entries(entries).map(([cultureCode, entry]) => [cultureCode, entry.value ?? ""]));
};

export function LocalizationAdminManager({
  adapter,
  className
}: LocalizationAdminManagerProps) {
  const [activeTab, setActiveTab] = useState<AdminLocalizationTab>("languages");
  const [cultures, setCultures] = useState<SupportedCulture[]>([]);
  const [languageForm, setLanguageForm] = useState<UpsertSupportedCultureRequest>(emptyCultureForm);
  const [editingLanguage, setEditingLanguage] = useState(false);
  const [loadingLanguages, setLoadingLanguages] = useState(true);
  const [savingLanguage, setSavingLanguage] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [translationSearchText, setTranslationSearchText] = useState("");
  const [submittedTranslationSearch, setSubmittedTranslationSearch] = useState("");
  const [selectedTreeFilterValue, setSelectedTreeFilterValue] = useState("");
  const [translations, setTranslations] = useState<LocalizationTranslation[]>([]);
  const [translationsTotal, setTranslationsTotal] = useState(0);
  const [translationsLoading, setTranslationsLoading] = useState(false);
  const [translationDrafts, setTranslationDrafts] = useState<Record<string, Record<string, string>>>({});
  const [savingTranslationId, setSavingTranslationId] = useState<string | null>(null);
  const [generatingTranslation, setGeneratingTranslation] = useState<TranslationGenerationState | null>(null);
  const [canGenerateTranslations, setCanGenerateTranslations] = useState(false);
  const [translationPage, setTranslationPage] = useState(1);
  const [translationPageSize, setTranslationPageSize] = useState(10);
  const [translationTree, setTranslationTree] = useState<LocalizationTranslationTreeNode[]>([]);
  const [translationTreeLoaded, setTranslationTreeLoaded] = useState(false);
  const [expandedTranslation, setExpandedTranslation] = useState<ExpandedTranslationState | null>(null);

  const sortedCultures = useMemo(
    () => [...cultures].sort((left, right) => left.sortOrder - right.sortOrder || left.displayName.localeCompare(right.displayName)),
    [cultures]
  );

  const defaultCultureCode = useMemo(
    () => sortedCultures.find((culture) => culture.isDefault)?.cultureCode.toLowerCase() ?? "en",
    [sortedCultures]
  );

  const treeOptions = useMemo(() => flattenTreeOptions(translationTree), [translationTree]);
  const selectedTreeFilter = useMemo(
    () => treeOptions.find((option) => option.value === selectedTreeFilterValue) ?? null,
    [selectedTreeFilterValue, treeOptions]
  );
  const translationPageCount = Math.max(1, Math.ceil(translationsTotal / translationPageSize));

  const loadCultures = async () => {
    setLoadingLanguages(true);
    setError(null);

    try {
      const nextCultures = await adapter.getCultures();
      setCultures(nextCultures ?? []);
    } catch (loadError) {
      console.error(loadError);
      setError("Unable to load supported languages.");
    } finally {
      setLoadingLanguages(false);
    }
  };

  const loadTranslationTree = async () => {
    try {
      const nextTree = await adapter.getTranslationTree();
      setTranslationTree(nextTree ?? []);
      setTranslationTreeLoaded(true);
    } catch (loadError) {
      console.error(loadError);
      setError("Unable to load translation key browser.");
    }
  };

  const loadTranslations = async () => {
    setTranslationsLoading(true);
    setError(null);

    try {
      const response = await adapter.getTranslations({
        search: submittedTranslationSearch || undefined,
        keyPrefix: selectedTreeFilter?.mode === "prefix" ? selectedTreeFilter.keyPrefix : undefined,
        exactKey: selectedTreeFilter?.mode === "exact" ? selectedTreeFilter.exactKey : undefined,
        page: translationPage,
        pageSize: translationPageSize
      });

      const items = response.items ?? [];
      setTranslations(items);
      setTranslationsTotal(response.totalCount ?? items.length);
      setTranslationDrafts(Object.fromEntries(items.map((item) => [item.translationKeyId, normalizeDraftValues(item)])));
      setCanGenerateTranslations(Boolean(response.canGenerateTranslations));
    } catch (loadError) {
      console.error(loadError);
      setError("Unable to load translations.");
    } finally {
      setTranslationsLoading(false);
    }
  };

  useEffect(() => {
    void loadCultures();
  }, []);

  useEffect(() => {
    if (activeTab !== "translations") {
      return;
    }

    if (!translationTreeLoaded) {
      void loadTranslationTree();
    }

    void loadTranslations();
  }, [activeTab, submittedTranslationSearch, selectedTreeFilterValue, translationPage, translationPageSize]);

  const startNewLanguage = () => {
    setLanguageForm(emptyCultureForm);
    setEditingLanguage(true);
    setMessage(null);
    setError(null);
  };

  const editLanguage = (culture: SupportedCulture) => {
    setLanguageForm({
      cultureCode: culture.cultureCode,
      displayName: culture.displayName,
      nativeName: culture.nativeName,
      isEnabled: culture.isEnabled,
      isDefault: culture.isDefault,
      sortOrder: culture.sortOrder
    });
    setEditingLanguage(true);
    setMessage(null);
    setError(null);
  };

  const backToLanguageList = () => {
    setLanguageForm(emptyCultureForm);
    setEditingLanguage(false);
    setMessage(null);
    setError(null);
  };

  const saveLanguage = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (savingLanguage) {
      return;
    }

    setSavingLanguage(true);
    setMessage(null);
    setError(null);

    try {
      await adapter.upsertCulture({
        ...languageForm,
        cultureCode: languageForm.cultureCode.trim().toLowerCase(),
        displayName: languageForm.displayName.trim(),
        nativeName: languageForm.nativeName.trim()
      });

      setMessage("Language saved.");
      setLanguageForm(emptyCultureForm);
      setEditingLanguage(false);
      await loadCultures();
    } catch (saveError) {
      console.error(saveError);
      setError("Unable to save language.");
    } finally {
      setSavingLanguage(false);
    }
  };

  const updateTranslationDraft = (translationKeyId: string, cultureCode: string, value: string) => {
    setTranslationDrafts((current) => ({
      ...current,
      [translationKeyId]: {
        ...(current[translationKeyId] ?? {}),
        [cultureCode]: value
      }
    }));
  };

  const syncUpdatedTranslation = (updated: LocalizationTranslation) => {
    const normalizedValues = normalizeDraftValues(updated);

    setTranslations((current) => current.map((item) => item.translationKeyId === updated.translationKeyId ? updated : item));
    setTranslationDrafts((current) => ({
      ...current,
      [updated.translationKeyId]: normalizedValues
    }));
    setExpandedTranslation((current) => current?.translationKeyId === updated.translationKeyId ? {
      ...current,
      values: normalizedValues,
      entries: normalizeEntries(updated)
    } : current);
  };

  const saveTranslation = async (translation: LocalizationTranslation) => {
    if (savingTranslationId) {
      return;
    }

    setSavingTranslationId(translation.translationKeyId);
    setMessage(null);
    setError(null);

    try {
      const updated = await adapter.upsertTranslation(translation.translationKeyId, {
        values: translationDrafts[translation.translationKeyId] ?? {}
      });

      syncUpdatedTranslation(updated);
      setMessage("Translation saved.");
    } catch (saveError) {
      console.error(saveError);
      setError("Unable to save translation.");
    } finally {
      setSavingTranslationId(null);
    }
  };

  const canGenerateForCulture = (culture: SupportedCulture) =>
    Boolean(adapter.generateTranslation) &&
    culture.cultureCode.toLowerCase() !== defaultCultureCode;

  const isGeneratingCulture = (translationKeyId: string, cultureCode: string) =>
    generatingTranslation?.translationKeyId === translationKeyId &&
    generatingTranslation.cultureCode === cultureCode;

  const generateTranslation = async (translationKeyId: string, culture: SupportedCulture) => {
    if (!adapter.generateTranslation || !canGenerateTranslations || generatingTranslation) {
      return;
    }

    setGeneratingTranslation({
      translationKeyId,
      cultureCode: culture.cultureCode
    });
    setMessage(null);
    setError(null);

    try {
      const updated = await adapter.generateTranslation(translationKeyId, {
        cultureCode: culture.cultureCode
      });

      syncUpdatedTranslation(updated);
      setMessage(`${culture.displayName} translation updated.`);
    } catch (generationError) {
      console.error(generationError);
      setError(`Unable to generate ${culture.displayName} translation.`);
    } finally {
      setGeneratingTranslation(null);
    }
  };

  const openExpandedTranslation = (translation: LocalizationTranslation) => {
    setExpandedTranslation({
      translationKeyId: translation.translationKeyId,
      key: translation.key,
      defaultValue: translation.defaultValue,
      values: { ...(translationDrafts[translation.translationKeyId] ?? normalizeDraftValues(translation)) },
      entries: normalizeEntries(translation)
    });
  };

  const applyExpandedTranslation = () => {
    if (!expandedTranslation) {
      return;
    }

    setTranslationDrafts((current) => ({
      ...current,
      [expandedTranslation.translationKeyId]: { ...expandedTranslation.values }
    }));
    setExpandedTranslation(null);
  };

  const submitTranslationSearch = () => {
    setTranslationPage(1);
    setSubmittedTranslationSearch(translationSearchText.trim());
  };

  const clearTreeFilter = () => {
    setTranslationPage(1);
    setSelectedTreeFilterValue("");
  };

  const getTranslationEntry = (translation: LocalizationTranslation, cultureCode: string) => {
    return normalizeEntries(translation)[cultureCode];
  };

  const renderSourceIndicator = (entry: LocalizationTranslationValue | undefined, culture: SupportedCulture) => {
    if (!entry?.isMachineTranslated || !entry.value?.trim()) {
      return null;
    }

    return (
      <span
        className="badge rounded-pill text-bg-info d-inline-flex align-items-center justify-content-center p-1"
        role="img"
        title={`AI-generated ${culture.displayName} translation`}
        aria-label={`AI-generated ${culture.displayName} translation`}
      >
        <AiGeneratedIndicatorIcon />
      </span>
    );
  };

  const renderLanguages = () => {
    if (editingLanguage) {
      return (
        <div className="card border-0 shadow-sm">
          <div className="card-body p-4">
            <h2 className="h5 mb-3">{languageForm.cultureCode ? `Edit ${languageForm.displayName || languageForm.cultureCode}` : "Add language"}</h2>
            <form onSubmit={saveLanguage}>
              <div className="row g-3">
                <div className="col-12 col-md-4">
                  <label className="form-label" htmlFor="cultureCode">Culture code</label>
                  <input
                    id="cultureCode"
                    className="form-control"
                    value={languageForm.cultureCode}
                    onChange={(event) => setLanguageForm((current) => ({ ...current, cultureCode: event.target.value }))}
                    placeholder="en"
                    required
                    maxLength={10}
                  />
                </div>
                <div className="col-12 col-md-4">
                  <label className="form-label" htmlFor="displayName">Display name</label>
                  <input
                    id="displayName"
                    className="form-control"
                    value={languageForm.displayName}
                    onChange={(event) => setLanguageForm((current) => ({ ...current, displayName: event.target.value }))}
                    placeholder="English"
                    required
                  />
                </div>
                <div className="col-12 col-md-4">
                  <label className="form-label" htmlFor="nativeName">Native name</label>
                  <input
                    id="nativeName"
                    className="form-control"
                    value={languageForm.nativeName}
                    onChange={(event) => setLanguageForm((current) => ({ ...current, nativeName: event.target.value }))}
                    placeholder="English"
                    required
                  />
                </div>
                <div className="col-12 col-md-4">
                  <label className="form-label" htmlFor="sortOrder">Sort order</label>
                  <input
                    id="sortOrder"
                    className="form-control"
                    type="number"
                    value={languageForm.sortOrder}
                    onChange={(event) => setLanguageForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))}
                  />
                </div>
                <div className="col-12 col-md-4 d-flex align-items-end">
                  <div className="form-check mb-2">
                    <input
                      id="isEnabled"
                      className="form-check-input"
                      type="checkbox"
                      checked={languageForm.isEnabled}
                      onChange={(event) => setLanguageForm((current) => ({ ...current, isEnabled: event.target.checked }))}
                    />
                    <label className="form-check-label" htmlFor="isEnabled">Enabled in language switcher</label>
                  </div>
                </div>
                <div className="col-12 col-md-4 d-flex align-items-end">
                  <div className="form-check mb-2">
                    <input
                      id="isDefault"
                      className="form-check-input"
                      type="checkbox"
                      checked={languageForm.isDefault}
                      onChange={(event) => setLanguageForm((current) => ({ ...current, isDefault: event.target.checked }))}
                    />
                    <label className="form-check-label" htmlFor="isDefault">Default language</label>
                  </div>
                </div>
              </div>
              <div className="d-flex gap-2 justify-content-between mt-4">
                <button className="btn btn-outline-secondary" type="button" onClick={backToLanguageList} disabled={savingLanguage}>
                  Back
                </button>
                <button className="btn btn-primary" type="submit" disabled={savingLanguage}>
                  {savingLanguage ? "Saving language..." : "Save language"}
                </button>
              </div>
            </form>
          </div>
        </div>
      );
    }

    return (
      <div className="card border-0 shadow-sm">
        <div className="card-body p-0">
          <div className="p-4 border-bottom d-flex flex-column flex-md-row gap-3 justify-content-between align-items-md-center">
            <div>
              <h2 className="h5 mb-1">Configured languages</h2>
              <p className="text-muted mb-0">Disabled languages stay configured but are hidden from users.</p>
            </div>
            <button className="btn btn-primary" type="button" onClick={startNewLanguage}>Add language</button>
          </div>
          {loadingLanguages ? (
            <div className="p-4 text-muted">Loading languages...</div>
          ) : (
            <div className="table-responsive">
              <table className="table mb-0 align-middle">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Name</th>
                    <th>Status</th>
                    <th>Default</th>
                    <th>Sort</th>
                    <th className="text-end">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {sortedCultures.map((culture) => (
                    <tr key={culture.cultureCode}>
                      <td className="fw-semibold text-uppercase">{culture.cultureCode}</td>
                      <td>
                        <div>{culture.displayName}</div>
                        <div className="text-muted small">{culture.nativeName}</div>
                      </td>
                      <td>
                        <span className={`badge ${culture.isEnabled ? "text-bg-success" : "text-bg-secondary"}`}>
                          {culture.isEnabled ? "Enabled" : "Disabled"}
                        </span>
                      </td>
                      <td>{culture.isDefault ? "Yes" : "No"}</td>
                      <td>{culture.sortOrder}</td>
                      <td className="text-end">
                        <button className="btn btn-outline-primary btn-sm" type="button" onClick={() => editLanguage(culture)}>
                          Edit
                        </button>
                      </td>
                    </tr>
                  ))}
                  {sortedCultures.length === 0 && (
                    <tr>
                      <td colSpan={6} className="p-4 text-muted">No languages configured.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    );
  };

  const renderTranslations = () => (
    <div className="card border-0 shadow-sm w-100">
      <div className="card-body p-0">
        <div className="p-4 border-bottom">
          <div className="row g-3 align-items-end">
            <div className="col-12 col-lg-5">
              <label className="form-label" htmlFor="translationSearch">Search</label>
              <input
                id="translationSearch"
                className="form-control"
                value={translationSearchText}
                onChange={(event) => setTranslationSearchText(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    event.preventDefault();
                    submitTranslationSearch();
                  }
                }}
                placeholder="Search key, default text, or translation..."
              />
            </div>
            <div className="col-12 col-lg-4">
              <label className="form-label" htmlFor="translationTree">Browse key tree</label>
              <select
                id="translationTree"
                className="form-select"
                value={selectedTreeFilterValue}
                onChange={(event) => {
                  setTranslationPage(1);
                  setSelectedTreeFilterValue(event.target.value);
                }}
              >
                <option value="">All keys</option>
                {treeOptions.map((option) => (
                  <option key={option.value} value={option.value}>{option.label}</option>
                ))}
              </select>
            </div>
            <div className="col-12 col-lg-3 d-flex gap-2">
              <button className="btn btn-primary flex-fill" type="button" onClick={submitTranslationSearch} disabled={translationsLoading}>
                Search
              </button>
              <button className="btn btn-outline-secondary" type="button" onClick={clearTreeFilter} disabled={!selectedTreeFilterValue || translationsLoading}>
                Clear tree
              </button>
            </div>
          </div>
        </div>

        <div className="px-4 py-3 border-bottom d-flex flex-column flex-md-row gap-3 justify-content-between align-items-md-center text-muted small">
          <div>Showing {translations.length} of {translationsTotal} translation keys.</div>
          <div className="d-flex align-items-center gap-2">
            <label className="mb-0" htmlFor="translationPageSize">Items</label>
            <select
              id="translationPageSize"
              className="form-select form-select-sm"
              style={{ width: 92 }}
              value={translationPageSize}
              onChange={(event) => {
                setTranslationPage(1);
                setTranslationPageSize(Number(event.target.value));
              }}
            >
              {[10, 20, 50, 100].map((pageSize) => (
                <option key={pageSize} value={pageSize}>{pageSize}</option>
              ))}
            </select>
          </div>
        </div>

        {translationsLoading ? (
          <div className="p-4 text-muted">Loading translations...</div>
        ) : translations.length === 0 ? (
          <div className="p-4 text-muted">No translation keys found.</div>
        ) : (
          <div className="p-4 d-flex flex-column gap-3">
            {translations.map((translation) => (
              <section className="border rounded-3 p-3 p-md-4" key={translation.translationKeyId}>
                <div className="small text-muted mb-1">Key</div>
                <code className="d-block text-break mb-3">{translation.key}</code>

                <div className="small text-muted mb-1">Default</div>
                <div className="mb-3 text-break">{translation.defaultValue}</div>

                <div className="small text-muted mb-2">Translations</div>
                <div className="d-flex flex-column gap-2">
                  {sortedCultures.map((culture) => {
                    const entry = getTranslationEntry(translation, culture.cultureCode);
                    const isGenerating = isGeneratingCulture(translation.translationKeyId, culture.cultureCode);

                    return (
                      <div className="input-group" key={`${translation.translationKeyId}-${culture.cultureCode}`}>
                        <span className="input-group-text d-flex justify-content-between align-items-center gap-2" style={{ minWidth: 112 }}>
                          <span className="text-uppercase fw-semibold">{culture.cultureCode}</span>
                          {renderSourceIndicator(entry, culture)}
                        </span>
                        <textarea
                          className="form-control"
                          rows={1}
                          placeholder=""
                          value={translationDrafts[translation.translationKeyId]?.[culture.cultureCode] ?? ""}
                          onChange={(event) => updateTranslationDraft(translation.translationKeyId, culture.cultureCode, event.target.value)}
                        />
                        {canGenerateForCulture(culture) && (
                          <button
                            className="btn btn-outline-secondary d-inline-flex align-items-center justify-content-center"
                            type="button"
                            aria-label={`Get ${culture.displayName} translation`}
                            title={canGenerateTranslations ? `Get ${culture.displayName} translation` : "AI translation is not configured"}
                            disabled={!canGenerateTranslations || Boolean(generatingTranslation) || Boolean(savingTranslationId)}
                            onClick={() => void generateTranslation(translation.translationKeyId, culture)}
                          >
                            {isGenerating ? "..." : <AutoTranslateIcon />}
                          </button>
                        )}
                      </div>
                    );
                  })}
                </div>

                <div className="d-flex flex-column flex-md-row justify-content-between align-items-md-end gap-3 mt-3">
                  <button className="btn btn-outline-secondary btn-sm align-self-start" type="button" onClick={() => openExpandedTranslation(translation)}>
                    Expand
                  </button>
                  <button
                    className="btn btn-primary btn-sm align-self-md-end"
                    type="button"
                    disabled={savingTranslationId !== null || generatingTranslation !== null}
                    onClick={() => void saveTranslation(translation)}
                  >
                    {savingTranslationId === translation.translationKeyId ? "Saving..." : "Save"}
                  </button>
                </div>
              </section>
            ))}
          </div>
        )}

        <div className="px-4 py-3 border-top d-flex flex-column flex-md-row gap-3 justify-content-between align-items-md-center">
          <div className="small text-muted">Page {translationPage} of {translationPageCount}</div>
          <div className="d-flex gap-2">
            <button
              className="btn btn-outline-secondary btn-sm"
              type="button"
              disabled={translationPage <= 1 || translationsLoading}
              onClick={() => setTranslationPage((current) => Math.max(1, current - 1))}
            >
              Previous
            </button>
            <button
              className="btn btn-outline-secondary btn-sm"
              type="button"
              disabled={translationPage >= translationPageCount || translationsLoading}
              onClick={() => setTranslationPage((current) => Math.min(translationPageCount, current + 1))}
            >
              Next
            </button>
          </div>
        </div>
      </div>

      {expandedTranslation && (
        <div className="modal d-block" tabIndex={-1} role="dialog" aria-modal="true" aria-labelledby="bt-localization-translation-title">
          <div className="modal-backdrop show" />
          <div className="modal-dialog modal-lg modal-dialog-centered position-relative" style={{ zIndex: 1060 }} role="document">
            <div className="modal-content shadow-lg">
              <div className="modal-header">
                <div>
                  <h2 className="modal-title h5 mb-1" id="bt-localization-translation-title">Edit Translation</h2>
                  <div className="small text-muted text-break">{expandedTranslation.key}</div>
                </div>
                <button type="button" className="btn-close" aria-label="Close" onClick={() => setExpandedTranslation(null)} />
              </div>
              <div className="modal-body">
                <div className="small text-muted mb-1">Default</div>
                <div className="mb-3 text-break">{expandedTranslation.defaultValue}</div>
                <div className="d-flex flex-column gap-3">
                  {sortedCultures.map((culture) => {
                    const isGenerating = isGeneratingCulture(expandedTranslation.translationKeyId, culture.cultureCode);

                    return (
                      <div key={`${expandedTranslation.translationKeyId}-${culture.cultureCode}`}>
                        <label className="form-label d-flex align-items-center gap-2 small fw-semibold">
                          <span className="text-uppercase">{culture.cultureCode}</span>
                          {renderSourceIndicator(expandedTranslation.entries[culture.cultureCode], culture)}
                        </label>
                        <div className="input-group">
                          <textarea
                            className="form-control"
                            rows={3}
                            placeholder=""
                            value={expandedTranslation.values[culture.cultureCode] ?? ""}
                            onChange={(event) => setExpandedTranslation((current) => current ? {
                              ...current,
                              values: {
                                ...current.values,
                                [culture.cultureCode]: event.target.value
                              }
                            } : current)}
                          />
                          {canGenerateForCulture(culture) && (
                            <button
                              className="btn btn-outline-secondary d-inline-flex align-items-center justify-content-center"
                              type="button"
                              aria-label={`Get ${culture.displayName} translation`}
                              title={canGenerateTranslations ? `Get ${culture.displayName} translation` : "AI translation is not configured"}
                              disabled={!canGenerateTranslations || Boolean(generatingTranslation) || Boolean(savingTranslationId)}
                              onClick={() => void generateTranslation(expandedTranslation.translationKeyId, culture)}
                            >
                              {isGenerating ? "..." : <AutoTranslateIcon />}
                            </button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
              <div className="modal-footer justify-content-between">
                <button type="button" className="btn btn-outline-secondary" onClick={() => setExpandedTranslation(null)}>
                  Close
                </button>
                <button type="button" className="btn btn-primary" onClick={applyExpandedTranslation}>
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );

  return (
    <div className={["w-100", className].filter(Boolean).join(" ")}>
      {message && <div className="alert alert-success">{message}</div>}
      {error && <div className="alert alert-danger">{error}</div>}

      <ul className="nav nav-tabs mb-4">
        <li className="nav-item">
          <button
            className={`nav-link ${activeTab === "languages" ? "active" : ""}`}
            type="button"
            onClick={() => setActiveTab("languages")}
          >
            Languages
          </button>
        </li>
        <li className="nav-item">
          <button
            className={`nav-link ${activeTab === "translations" ? "active" : ""}`}
            type="button"
            onClick={() => setActiveTab("translations")}
          >
            Translations
          </button>
        </li>
      </ul>

      {activeTab === "languages" ? renderLanguages() : renderTranslations()}
    </div>
  );
}
