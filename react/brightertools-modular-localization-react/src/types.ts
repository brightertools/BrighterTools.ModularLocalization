export interface SupportedCulture {
  cultureCode: string;
  displayName: string;
  nativeName: string;
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
}

export interface UpsertSupportedCultureRequest {
  cultureCode: string;
  displayName: string;
  nativeName: string;
  isEnabled: boolean;
  isDefault: boolean;
  sortOrder: number;
}

export interface LocalizationTranslationValue {
  value: string;
  isMachineTranslated: boolean;
  updatedAtUtc?: string | null;
}

export interface LocalizationTranslation {
  translationKeyId: string;
  key: string;
  defaultValue: string;
  values: Record<string, string>;
  entries?: Record<string, LocalizationTranslationValue>;
  updatedAtUtc: string;
}

export interface LocalizationTranslationListRequest {
  search?: string;
  keyPrefix?: string;
  exactKey?: string;
  page?: number;
  pageSize?: number;
}

export interface LocalizationTranslationListResponse {
  items: LocalizationTranslation[];
  totalCount: number;
  page: number;
  pageSize: number;
  canGenerateTranslations?: boolean;
}

export interface LocalizationTranslationTreeNode {
  label: string;
  fullKey: string;
  isLeaf: boolean;
  children: LocalizationTranslationTreeNode[];
}

export interface UpsertLocalizationTranslationRequest {
  values: Record<string, string>;
}

export interface GenerateLocalizationTranslationRequest {
  cultureCode: string;
  sourceCulture?: string;
}

export interface LocalizationAdminAdapter {
  getCultures: () => Promise<SupportedCulture[]>;
  upsertCulture: (request: UpsertSupportedCultureRequest) => Promise<SupportedCulture>;
  getTranslations: (request: LocalizationTranslationListRequest) => Promise<LocalizationTranslationListResponse>;
  getTranslationTree: () => Promise<LocalizationTranslationTreeNode[]>;
  upsertTranslation: (translationKeyId: string, request: UpsertLocalizationTranslationRequest) => Promise<LocalizationTranslation>;
  generateTranslation?: (translationKeyId: string, request: GenerateLocalizationTranslationRequest) => Promise<LocalizationTranslation>;
}
