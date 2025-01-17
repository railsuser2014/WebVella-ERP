﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WebVella.ERP.Api;
using WebVella.ERP.Api.Models;
using WebVella.ERP.Api.Models.AutoMapper;
using WebVella.ERP.Storage;
using WebVella.ERP.Utilities.Dynamic;

namespace WebVella.ERP.Api
{
    public class EntityManager
    {
        public IStorageService Storage { get; set; }

        public IStorageEntityRepository EntityRepository { get; set; }

        public IStorageObjectFactory StorageObjectFactory { get; set; }

        public EntityManager(IStorageService storage)
        {
            Storage = storage;
            EntityRepository = storage.GetEntityRepository();
            StorageObjectFactory = storage.GetObjectFactory();
        }
         
        #region << Validation methods >>

        private List<ErrorModel> ValidateEntity(Entity entity, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            IList<IStorageEntity> entities = EntityRepository.Read();

            if (entity.Id == Guid.Empty)
                errorList.Add(new ErrorModel("id", null, "Id is required!"));

            if (checkId)
            {
                //update
                if (entity.Id != Guid.Empty)
                {
                    IStorageEntity verifiedEntity = EntityRepository.Read(entity.Id);

                    if (verifiedEntity == null)
                        errorList.Add(new ErrorModel("id", entity.Id.ToString(), "Entity with such Id does not exist!"));
                }
            }
            else
            {
                //create

            }

            errorList.AddRange(ValidationUtility.ValidateName(entity.Name));

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                IStorageEntity verifiedEntity = EntityRepository.Read(entity.Name);

                if (verifiedEntity != null && verifiedEntity.Id != entity.Id)
                    errorList.Add(new ErrorModel("name", entity.Name, "Entity with such Name exists already!"));
            }

            errorList.AddRange(ValidationUtility.ValidateLabel(entity.Label));

            errorList.AddRange(ValidationUtility.ValidateLabelPlural(entity.LabelPlural));

            if (entity.RecordPermissions != null)
            {
                if (entity.RecordPermissions.CanRead == null || entity.RecordPermissions.CanRead.Count == 0)
                    errorList.Add(new ErrorModel("permissions.canRead", null, "CanRead is required! It must contains at least one item!"));

                if (entity.RecordPermissions.CanRead == null || entity.RecordPermissions.CanRead.Count == 0)
                    errorList.Add(new ErrorModel("permissions.canCreate", null, "CanCreate is required! It must contains at least one item!"));

                if (entity.RecordPermissions.CanUpdate == null || entity.RecordPermissions.CanUpdate.Count == 0)
                    errorList.Add(new ErrorModel("permissions.canUpdate", null, "CanUpdate is required! It must contains at least one item!"));

                if (entity.RecordPermissions.CanDelete == null || entity.RecordPermissions.CanDelete.Count == 0)
                    errorList.Add(new ErrorModel("permissions.canDelete", null, "CanDelete is required! It must contains at least one item!"));
            }
            else
                errorList.Add(new ErrorModel("permissions", null, "Permissions is required!"));

            if (string.IsNullOrWhiteSpace(entity.IconName))
                entity.IconName = "database";

            return errorList;
        }

        private List<ErrorModel> ValidateFields(Guid entityId, List<InputField> fields, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);
            Entity entity = storageEntity.MapTo<Entity>();

            if (fields.Count == 0)
            {
                errorList.Add(new ErrorModel("fields", null, "There should be at least one field!"));
                return errorList;
            }

            int primaryFieldCount = 0;

            foreach (var field in fields)
            {
                errorList.AddRange(ValidateField(entity, field, false));

                if (field is InputGuidField)
                {
                    primaryFieldCount++;
                }
            }

            if (primaryFieldCount < 1)
                errorList.Add(new ErrorModel("fields.id", null, "Must have one unique identifier field!"));

            if (primaryFieldCount > 1)
                errorList.Add(new ErrorModel("fields.id", null, "Too many primary fields. Must have only one unique identifier!"));

            return errorList;
        }

        private List<ErrorModel> ValidateField(Entity entity, InputField field, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            if (field.Id == Guid.Empty)
                errorList.Add(new ErrorModel("id", null, "Id is required!"));

            int fieldsSameIdCount = entity.Fields.Where(f => f.Id == field.Id).Count();

            if ((checkId && fieldsSameIdCount > 1) || (!checkId && fieldsSameIdCount > 0))
                errorList.Add(new ErrorModel("id", null, "There is already a field with such Id!"));

            int fieldsSameNameCount = entity.Fields.Where(f => f.Name == field.Name).Count();

            if ((checkId && fieldsSameNameCount > 1) || (!checkId && fieldsSameNameCount > 0))
                errorList.Add(new ErrorModel("name", null, "There is already a field with such Name!"));

            errorList.AddRange(ValidationUtility.ValidateName(field.Name));

            errorList.AddRange(ValidationUtility.ValidateLabel(field.Label));

            if (field is InputAutoNumberField)
            {
                if (field.Required.HasValue && field.Required.Value && !((InputAutoNumberField)field).DefaultValue.HasValue)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (((AutoNumberField)field).DisplayFormat == null)
                //    errorList.Add(new ErrorModel("DisplayFormat", null, "DisplayFormat is required!"));

                //if (!((AutoNumberField)field).StartingNumber.HasValue)
                //    errorList.Add(new ErrorModel("startingNumber", null, "Starting Number is required!"));

                //TODO:parse DisplayFormat field
            }
            else if (field is InputCheckboxField)
            {
                if (!((InputCheckboxField)field).DefaultValue.HasValue)
                    ((InputCheckboxField)field).DefaultValue = false;
            }
            else if (field is InputCurrencyField)
            {
                if (field.Required.HasValue && field.Required.Value && !((InputCurrencyField)field).DefaultValue.HasValue)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((CurrencyField)field).MinValue.HasValue)
                //    errorList.Add(new ErrorModel("minValue", null, "Min Value is required!"));

                //if (!((CurrencyField)field).MaxValue.HasValue)
                //    errorList.Add(new ErrorModel("maxValue", null, "Max Value is required!"));

                //if (((CurrencyField)field).MinValue.HasValue && ((CurrencyField)field).MaxValue.HasValue)
                //{
                //    if (((CurrencyField)field).MinValue.Value >= ((CurrencyField)field).MaxValue.Value)
                //        errorList.Add(new ErrorModel("MinValue", null, "Min Value must be less than Max Value!"));
                //}
            }
            else if (field is InputDateField)
            {
                //TODO:parse format and check if it is valid

                if (string.IsNullOrWhiteSpace( ((InputDateField)field).Format) )
                    errorList.Add(new ErrorModel("format", null, "Date format is required!"));

                if (!((InputDateField)field).UseCurrentTimeAsDefaultValue.HasValue)
                    ((InputDateField)field).UseCurrentTimeAsDefaultValue = false;
                //errorList.Add(new ErrorModel("useCurrentTimeAsDefaultValue", null, "Use current Time is required!"));
            }
            else if (field is InputDateTimeField)
            {
                //TODO:parse format and check if it is valid

                if (string.IsNullOrWhiteSpace(((InputDateTimeField)field).Format))
                    errorList.Add(new ErrorModel("format", null, "Datetime format is required!"));

                if (!((InputDateTimeField)field).UseCurrentTimeAsDefaultValue.HasValue)
                    ((InputDateTimeField)field).UseCurrentTimeAsDefaultValue = false;
                //errorList.Add(new ErrorModel("useCurrentTimeAsDefaultValue", null, "Use current Time is required!"));
            }
            else if (field is InputEmailField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputEmailField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((EmailField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));
            }
            else if (field is InputFileField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputFileField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));
            }
            //else if (field is FormulaField)
            //{
            //    if (!string.IsNullOrWhiteSpace(((FormulaField)field).FormulaText))
            //    {
            //        //TODO: parse formula text and check if it is valid
            //    }
            //    else
            //        errorList.Add(new ErrorModel("fields.formulaText", null, "Formula Text is required!"));

            //    if (!((FormulaField)field).DecimalPlaces.HasValue)
            //        errorList.Add(new ErrorModel("fields.decimalPlaces", null, "Decimal Places is required!"));
            //}
            else if (field is InputGuidField)
            {
                if ((((InputGuidField)field).Unique.HasValue && ((InputGuidField)field).Unique.Value) &&
                   (!((InputGuidField)field).GenerateNewId.HasValue || !((InputGuidField)field).GenerateNewId.Value))
                    errorList.Add(new ErrorModel("defaultValue", null, "Generate New Id is required when the field is marked as unique!"));

                if ((((InputGuidField)field).Required.HasValue && ((InputGuidField)field).Required.Value) &&
                    (!((InputGuidField)field).GenerateNewId.HasValue || !((InputGuidField)field).GenerateNewId.Value) &&
                    ((InputGuidField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required when the field is marked as required and generate new id option is not selected!"));
            }
            else if (field is InputHtmlField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputHtmlField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));
            }
            else if (field is InputImageField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputImageField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));
            }
            else if (field is InputMultiLineTextField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputMultiLineTextField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((MultiLineTextField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));

                //if (!((MultiLineTextField)field).VisibleLineNumber.HasValue)
                //    errorList.Add(new ErrorModel("visibleLineNumber", null, "Visible Line Number is required!"));

                //if (((MultiLineTextField)field).VisibleLineNumber.HasValue && ((MultiLineTextField)field).VisibleLineNumber.Value > 20)
                //    errorList.Add(new ErrorModel("visibleLineNumber", null, "Visible Line Number cannot be greater than 20!"));
            }
            else if (field is InputMultiSelectField)
            {
                if (field.Required.HasValue && field.Required.Value &&
                    (((InputMultiSelectField)field).DefaultValue == null || ((InputMultiSelectField)field).DefaultValue.Count() == 0))
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                if (((InputMultiSelectField)field).Options != null)
                {
                    if (((InputMultiSelectField)field).Options.Count == 0)
                        errorList.Add(new ErrorModel("options", null, "Options must contains at least one item!"));
                }
                else
                    errorList.Add(new ErrorModel("options", null, "Options is required!"));
            }
            else if (field is InputNumberField)
            {
                if (field.Required.HasValue && field.Required.Value && !((InputNumberField)field).DefaultValue.HasValue)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((NumberField)field).MinValue.HasValue)
                //    errorList.Add(new ErrorModel("minValue", null, "Min Value is required!"));

                //if (!((NumberField)field).MaxValue.HasValue)
                //    errorList.Add(new ErrorModel("maxValue", null, "Max Value is required!"));

                //if (((NumberField)field).MinValue.HasValue && ((NumberField)field).MaxValue.HasValue)
                //{
                //    if (((NumberField)field).MinValue.Value >= ((NumberField)field).MaxValue.Value)
                //        errorList.Add(new ErrorModel("MinValue", null, "Min Value must be less than Max Value!"));
                //}

                if (!((InputNumberField)field).DecimalPlaces.HasValue)
                    ((InputNumberField)field).DecimalPlaces = 2;
                //errorList.Add(new ErrorModel("decimalPlaces", null, "Decimal Places is required!"));
            }
            else if (field is InputPasswordField)
            {
                //if (!((PasswordField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));

                if (!((InputPasswordField)field).Encrypted.HasValue)
                    ((InputPasswordField)field).Encrypted = true;
            }
            else if (field is InputPercentField)
            {
                if (field.Required.HasValue && field.Required.Value && !((InputPercentField)field).DefaultValue.HasValue)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((PercentField)field).MinValue.HasValue)
                //    errorList.Add(new ErrorModel("minValue", null, "Min Value is required!"));

                //if (!((PercentField)field).MaxValue.HasValue)
                //    errorList.Add(new ErrorModel("maxValue", null, "Max Value is required!"));

                //if (((PercentField)field).MinValue.HasValue && ((PercentField)field).MaxValue.HasValue)
                //{
                //    if (((PercentField)field).MinValue.Value >= ((PercentField)field).MaxValue.Value)
                //        errorList.Add(new ErrorModel("MinValue", null, "Min Value must be less than Max Value!"));
                //}

                if (!((InputPercentField)field).DecimalPlaces.HasValue)
                    ((InputPercentField)field).DecimalPlaces = 2;
                //errorList.Add(new ErrorModel("decimalPlaces", null, "Decimal Places is required!"));
            }
            else if (field is InputPhoneField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputPhoneField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!string.IsNullOrWhiteSpace(((PhoneField)field).Format))
                //    errorList.Add(new ErrorModel("format", null, "Format is required!"));

                //if (!((PhoneField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));

                //TODO: parse format and check if it is valid
            }
            else if (field is InputSelectField)
            {
                if (field.Required.HasValue && field.Required.Value && string.IsNullOrWhiteSpace(((InputSelectField)field).DefaultValue))
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                if (((InputSelectField)field).Options != null)
                {
                    if (((InputSelectField)field).Options.Count == 0)
                        errorList.Add(new ErrorModel("options", null, "Options must contains at least one item!"));
                }
                else
                    errorList.Add(new ErrorModel("options", null, "Options is required!"));
            }
            else if (field is InputTextField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputTextField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((TextField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));
            }
            else if (field is InputUrlField)
            {
                if (field.Required.HasValue && field.Required.Value && ((InputUrlField)field).DefaultValue == null)
                    errorList.Add(new ErrorModel("defaultValue", null, "Default Value is required!"));

                //if (!((UrlField)field).MaxLength.HasValue)
                //    errorList.Add(new ErrorModel("maxLength", null, "Max Length is required!"));

                if (!((InputUrlField)field).OpenTargetInNewWindow.HasValue)
                    ((InputUrlField)field).OpenTargetInNewWindow = false;
                //errorList.Add(new ErrorModel("openTargetInNewWindow", null, "Open Target In New Window is required!"));
            }

            return errorList;
        }

        private List<ErrorModel> ValidateRecordLists(Guid entityId, List<InputRecordList> recordLists, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);
            Entity entity = storageEntity.MapTo<Entity>();

            foreach (var recordList in recordLists)
            {
                errorList.AddRange(ValidateRecordList(entity, recordList, checkId));
            }

            return errorList;
        }

        private List<ErrorModel> ValidateRecordList(Entity entity, InputRecordList recordlist, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            List<IStorageEntity> storageEntityList = EntityRepository.Read();
            List<Entity> entities = storageEntityList.MapTo<Entity>();

            EntityRelationManager relationManager = new EntityRelationManager(Storage);
            EntityRelationListResponse relationListResponse = relationManager.Read();
            List<EntityRelation> relationList = new List<EntityRelation>();
            if (relationListResponse.Object != null)
                relationList = relationListResponse.Object;

            //List<RecordList> recordLists = new List<RecordList>();
            //List<RecordView> recordViews = new List<RecordView>();
            //List<Field> fields = new List<Field>();

            //foreach (var ent in entities)
            //{
            //	recordLists.AddRange(ent.RecordLists);
            //	recordViews.AddRange(ent.RecordViews);
            //	fields.AddRange(ent.Fields);
            //}

            if (!recordlist.Id.HasValue || recordlist.Id.Value == Guid.Empty)
                errorList.Add(new ErrorModel("id", null, "Id is required!"));

            if (checkId)
            {
                int listSameIdCount = entity.RecordLists.Where(f => f.Id == recordlist.Id).Count();

                if (listSameIdCount > 1)
                    errorList.Add(new ErrorModel("id", null, "There is already a list with such Id!"));

                int listSameNameCount = entity.Fields.Where(f => f.Name == recordlist.Name).Count();

                if (listSameNameCount > 1)
                    errorList.Add(new ErrorModel("name", null, "There is already a list with such Name!"));
            }

            errorList.AddRange(ValidationUtility.ValidateName(recordlist.Name));

            errorList.AddRange(ValidationUtility.ValidateLabel(recordlist.Label));

            if (!recordlist.Default.HasValue)
                recordlist.Default = false;
            if (!recordlist.System.HasValue)
                recordlist.System = false;
            if (!recordlist.Weight.HasValue)
                recordlist.Weight = 1;
            if (!recordlist.PageSize.HasValue)
                recordlist.PageSize = 10;
            if (recordlist.CssClass != null)
                recordlist.CssClass = recordlist.CssClass.Trim();

            if (recordlist.Type != null)
            {
                RecordListType type;
                if (!Enum.TryParse<RecordListType>(recordlist.Type, true, out type))
                    errorList.Add(new ErrorModel("type", recordlist.Type, "There is no such type!"));
            }
            else
                errorList.Add(new ErrorModel("type", recordlist.Type, "Type is required!"));

            if (recordlist.Columns != null && recordlist.Columns.Count > 0)
            {
                foreach (var column in recordlist.Columns)
                {
                    if (column is InputRecordListFieldItem)
                    {
                        InputRecordListFieldItem inputColumn = (InputRecordListFieldItem)column;
                        if (string.IsNullOrWhiteSpace(((InputRecordListFieldItem)column).FieldName) && ((InputRecordListFieldItem)column).FieldId == null)
                        {
                            errorList.Add(new ErrorModel("columns.fieldName", null, "Field name or id is required!"));
                        }
                        else
                        {
                            if (((InputRecordListFieldItem)column).FieldId == null)
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListFieldItem && ((InputRecordListFieldItem)i).FieldName == inputColumn.FieldName).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.fieldName", null, "There is already an item with such field name!"));

                                if (!entity.Fields.Any(f => f.Name == inputColumn.FieldName))
                                    errorList.Add(new ErrorModel("columns.fieldName", null, "Wrong name. There is no field with such name!"));
                                else
                                    inputColumn.FieldId = entity.Fields.FirstOrDefault(f => f.Name == inputColumn.FieldName).Id;
                            }
                            else if (string.IsNullOrWhiteSpace(((InputRecordListFieldItem)column).FieldName))
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListFieldItem && ((InputRecordListFieldItem)i).FieldId == inputColumn.FieldId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.fieldId", null, "There is already an item with such field identifier!"));

                                if (!entity.Fields.Any(f => f.Id == inputColumn.FieldId.Value))
                                    errorList.Add(new ErrorModel("columns.fieldId", null, "Wrong id. There is no field with such id!"));
                                else
                                    inputColumn.FieldName = entity.Fields.FirstOrDefault(f => f.Id == inputColumn.FieldId).Name;
                            }
                            else
                            {
                                //TODO validate if id does not fit the name

                                if (recordlist.Columns.Where(i => i is InputRecordListFieldItem && ((InputRecordListFieldItem)i).FieldId == inputColumn.FieldId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.fieldId", null, "There is already an item with such field identifier!"));

                                if (!entity.Fields.Any(f => f.Id == inputColumn.FieldId.Value))
                                    errorList.Add(new ErrorModel("columns.fieldId", null, "Wrong id. There is no field with such id!"));

                            }
                        }

                        inputColumn.EntityId = entity.Id;
                        inputColumn.EntityName = entity.Name;
                    }
                    else if (column is InputRecordListListItem)
                    {
                        InputRecordListListItem inputColumn = (InputRecordListListItem)column;
                        if (string.IsNullOrWhiteSpace(inputColumn.ListName) && inputColumn.ListId == null)
                        {
                            errorList.Add(new ErrorModel("columns.listName", null, "List name or id is required!"));
                        }
                        else
                        {
                            if (inputColumn.ListId == null)
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListListItem && ((InputRecordListListItem)i).ListName == inputColumn.ListName).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.listName", null, "There is already an item with such list name!"));

                                if (!entity.RecordLists.Any(f => f.Name == inputColumn.ListName))
                                    errorList.Add(new ErrorModel("columns.listName", null, "Wrong name. There is no list with such name!"));
                                else
                                    inputColumn.ListId = entity.RecordLists.FirstOrDefault(l => l.Name == inputColumn.ListName).Id;
                            }
                            else if (string.IsNullOrWhiteSpace(inputColumn.ListName))
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListListItem && ((InputRecordListListItem)i).ListId == inputColumn.ListId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.listId", null, "There is already an item with sane id!"));

                                if (!entity.RecordLists.Any(f => f.Id == inputColumn.ListId))
                                    errorList.Add(new ErrorModel("columns.listId", null, "Wrong list id. There is no list with such id!"));
                                else
                                    inputColumn.ListName = entity.RecordLists.FirstOrDefault(l => l.Id == inputColumn.ListId).Name;
                            }
                            else
                            {
                                //TODO validate if id does not fit the name

                                if (recordlist.Columns.Where(i => i is InputRecordListListItem && ((InputRecordListListItem)i).ListId == inputColumn.ListId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.listId", null, "There is already an item with sane id!"));

                                if (!entity.RecordLists.Any(f => f.Id == inputColumn.ListId))
                                    errorList.Add(new ErrorModel("columns.listId", null, "Wrong list id. There is no list with such id!"));
                            }

                        }

                        inputColumn.EntityId = entity.Id;
                        inputColumn.EntityName = entity.Name;
                    }
                    else if (column is InputRecordListViewItem)
                    {
                        InputRecordListViewItem inputColumn = (InputRecordListViewItem)column;
                        if (string.IsNullOrWhiteSpace(inputColumn.ViewName) && inputColumn.ViewId == null)
                        {
                            errorList.Add(new ErrorModel("columns.viewName", null, "View name or id is required!"));
                        }
                        else
                        {
                            if (inputColumn.ViewId == null)
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListViewItem && ((InputRecordListViewItem)i).ViewName == inputColumn.ViewName).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.viewName", null, "There is already an item with such view name!"));

                                if (!entity.RecordViews.Any(f => f.Name == inputColumn.ViewName))
                                    errorList.Add(new ErrorModel("columns.viewName", null, "Wrong name. There is no view with such name!"));
                                else
                                    inputColumn.ViewId = entity.RecordViews.FirstOrDefault(v => v.Name == inputColumn.ViewName).Id;
                            }
                            else if (string.IsNullOrWhiteSpace(inputColumn.ViewName))
                            {
                                if (recordlist.Columns.Where(i => i is InputRecordListViewItem && ((InputRecordListViewItem)i).ViewId == inputColumn.ViewId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.viewId", null, "There is already an item with such view id!"));

                                if (!entity.RecordViews.Any(f => f.Id == inputColumn.ViewId))
                                    errorList.Add(new ErrorModel("columns.viewId", null, "Wrong id. There is no view with such id!"));
                                else
                                    inputColumn.ViewName = entity.RecordViews.FirstOrDefault(v => v.Id == inputColumn.ViewId).Name;
                            }
                            else
                            {
                                //TODO validate if id does not fit the name

                                if (recordlist.Columns.Where(i => i is InputRecordListViewItem && ((InputRecordListViewItem)i).ViewId == inputColumn.ViewId).Count() > 1)
                                    errorList.Add(new ErrorModel("columns.viewId", null, "There is already an item with such view id!"));

                                if (!entity.RecordViews.Any(f => f.Id == inputColumn.ViewId))
                                    errorList.Add(new ErrorModel("columns.viewId", null, "Wrong id. There is no view with such id!"));
                            }
                        }

                        inputColumn.EntityId = entity.Id;
                        inputColumn.EntityName = entity.Name;
                    }
                    else if (column is InputRecordListRelationFieldItem)
                    {
                        InputRecordListRelationFieldItem inputColumn = (InputRecordListRelationFieldItem)column;
                        if (string.IsNullOrWhiteSpace(inputColumn.RelationName))
                        {
                            errorList.Add(new ErrorModel("columns.relationName", null, "Relation name is required!"));
                        }
                        else
                        {
                            if (!relationList.Any(r => r.Name == inputColumn.RelationName))
                                errorList.Add(new ErrorModel("columns.relationName", null, "Wrong name. There is no relation with such name!"));
                            else
                            {
                                inputColumn.RelationId = relationList.FirstOrDefault(r => r.Name == inputColumn.RelationName).Id;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(inputColumn.FieldName))
                        {
                            errorList.Add(new ErrorModel("columns.fieldName", null, "Field name is required!"));
                        }
                        else if (inputColumn.RelationId.HasValue && inputColumn.RelationId != Guid.Empty)
                        {
                            if (recordlist.Columns.Where(i => i is InputRecordListRelationFieldItem &&
                                ((InputRecordListRelationFieldItem)i).FieldName == inputColumn.FieldName &&
                                ((InputRecordListRelationFieldItem)i).RelationId == inputColumn.RelationId ).Count() > 1)
                                errorList.Add(new ErrorModel("columns.fieldName", null, "There is already an item with such field name!"));
                            else
                            {
                                EntityRelation relation = relationList.FirstOrDefault(r => r.Id == inputColumn.RelationId.Value);

                                if (relation != null)
                                {
                                    Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                    Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                    if (relEntity != null)
                                    {
                                        inputColumn.EntityId = entity.Id;
                                        inputColumn.EntityName = entity.Name;

                                        Field relField = relEntity.Fields.FirstOrDefault(f => f.Name == inputColumn.FieldName);

                                        if (relField != null)
                                            inputColumn.FieldId = relField.Id;
                                        else
                                            errorList.Add(new ErrorModel("columns.fieldName", null, "Wrong name. There is no field with such name!"));
                                    }
                                }
                            }
                        }
                    }

                    else if (column is InputRecordListRelationListItem)
                    {
                        InputRecordListRelationListItem inputColumn = (InputRecordListRelationListItem)column;
                        if (string.IsNullOrWhiteSpace(inputColumn.RelationName))
                        {
                            errorList.Add(new ErrorModel("columns.relationName", null, "Relation name is required!"));
                        }
                        else
                        {
                            if (!relationList.Any(r => r.Name == inputColumn.RelationName))
                                errorList.Add(new ErrorModel("columns.relationName", null, "Wrong name. There is no relation with such name!"));
                            else
                            {
                                inputColumn.RelationId = relationList.FirstOrDefault(r => r.Name == inputColumn.RelationName).Id;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(inputColumn.ListName))
                        {
                            errorList.Add(new ErrorModel("columns.listName", null, "List name is required!"));
                        }
                        else if (inputColumn.RelationId.HasValue && inputColumn.RelationId != Guid.Empty)
                        {
                            if (recordlist.Columns.Where(i => i is InputRecordListRelationListItem && ((InputRecordListRelationListItem)i).ListName == inputColumn.ListName).Count() > 1)
                                errorList.Add(new ErrorModel("columns.listName", null, "There is already an item with such list name!"));
                            else
                            {
                                EntityRelation relation = relationList.FirstOrDefault(r => r.Id == inputColumn.RelationId.Value);

                                if (relation != null)
                                {

                                    Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                    Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                    if (relEntity != null)
                                    {
                                        inputColumn.EntityId = entity.Id;
                                        inputColumn.EntityName = entity.Name;

                                        RecordList relList = relEntity.RecordLists.FirstOrDefault(l => l.Name == inputColumn.ListName);
                                        if (relList != null)
                                            inputColumn.ListId = relList.Id;
                                        else
                                            errorList.Add(new ErrorModel("columns.listId", null, "Wrong Id. There is no list with such id!"));
                                    }
                                }
                            }
                        }
                    }
                    else if (column is InputRecordListRelationViewItem)
                    {
                        InputRecordListRelationViewItem inputColumn = (InputRecordListRelationViewItem)column;
                        if (string.IsNullOrWhiteSpace(inputColumn.RelationName))
                        {
                            errorList.Add(new ErrorModel("columns.relationName", null, "Relation name is required!"));
                        }
                        else
                        {
                            if (!relationList.Any(r => r.Name == inputColumn.RelationName))
                                errorList.Add(new ErrorModel("columns.relationName", null, "Wrong name. There is no relation with such name!"));
                            else
                            {
                                inputColumn.RelationId = relationList.FirstOrDefault(r => r.Name == inputColumn.RelationName).Id;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(inputColumn.ViewName))
                        {
                            errorList.Add(new ErrorModel("columns.viewName", null, "View name is required!"));
                        }
                        else if (inputColumn.RelationId.HasValue && inputColumn.RelationId != Guid.Empty)
                        {
                            if (recordlist.Columns.Where(i => i is InputRecordListRelationViewItem && ((InputRecordListRelationViewItem)i).ViewName == inputColumn.ViewName).Count() > 1)
                                errorList.Add(new ErrorModel("columns.viewName", null, "There is already an item with such view name!"));
                            else
                            {
                                EntityRelation relation = relationList.FirstOrDefault(r => r.Id == inputColumn.RelationId.Value);

                                if (relation != null)
                                {
                                    Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                    Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                    if (relEntity != null)
                                    {
                                        inputColumn.EntityId = entity.Id;
                                        inputColumn.EntityName = entity.Name;

                                        RecordView relView = relEntity.RecordViews.FirstOrDefault(v => v.Name == inputColumn.ViewName);
                                        if (relView != null)
                                            inputColumn.ViewId = relView.Id;
                                        else
                                            errorList.Add(new ErrorModel("columns.viewName", null, "Wrong name. There is no view with such name!"));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (recordlist.Query != null)
            {
                List<ErrorModel> queryErrors = ValidateRecordListQuery(recordlist.Query);
                errorList.AddRange(queryErrors);
            }

            if (recordlist.Sorts != null)
            {
                foreach (var sort in recordlist.Sorts)
                {
                    if (string.IsNullOrWhiteSpace(sort.FieldName))
                        errorList.Add(new ErrorModel("sorts.fieldName", sort.FieldName, "FieldName is required!"));

                    if (string.IsNullOrWhiteSpace(sort.SortType))
                        errorList.Add(new ErrorModel("sorts.sortType", sort.SortType, "SortType is required!"));
                    else
                    {
                        QuerySortType sortType;
                        if (!Enum.TryParse<QuerySortType>(sort.SortType, true, out sortType))
                            errorList.Add(new ErrorModel("sorts.sortType", sort.SortType, "There is no such sort type!"));
                    }
                }
            }

            return errorList;
        }

        private List<ErrorModel> ValidateRecordListQuery(InputRecordListQuery query)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            if (string.IsNullOrWhiteSpace(query.QueryType))
                errorList.Add(new ErrorModel("query.queryType", query.QueryType, "QueryType is required!"));
            else
            {
                QueryType queryType;
                if (!Enum.TryParse<QueryType>(query.QueryType, true, out queryType))
                    errorList.Add(new ErrorModel("query.queryType", query.QueryType, "There is no such query type!"));
                else
                {
                    if (queryType != QueryType.AND && queryType != QueryType.OR && string.IsNullOrWhiteSpace(query.FieldName))
                        errorList.Add(new ErrorModel("query.fieldName", query.FieldName, "FieldName is required!"));

                    if (queryType != QueryType.AND && queryType != QueryType.OR && query.FieldValue == null)
                        errorList.Add(new ErrorModel("query.fieldValue", query.FieldValue, "FieldValue is required!"));

                    if ((queryType == QueryType.AND || queryType == QueryType.OR) && (query.SubQueries == null || query.SubQueries.Count == 0))
                        errorList.Add(new ErrorModel("query.subQueries", null, "SubQueries must have at least one item!"));

                    if (query.SubQueries != null && query.SubQueries.Count > 0)
                    {
                        foreach (var subQuery in query.SubQueries)
                        {
                            List<ErrorModel> subQueryErrors = ValidateRecordListQuery(subQuery);
                            errorList.AddRange(subQueryErrors);
                        }
                    }
                }
            }

            return errorList;
        }

        private List<ErrorModel> ValidateRecordViews(Guid entityId, List<InputRecordView> recordViewList, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);
            Entity entity = storageEntity.MapTo<Entity>();

            foreach (var recordView in recordViewList)
            {
                errorList.AddRange(ValidateRecordView(entity, recordView, checkId));
            }

            return errorList;
        }

        private List<ErrorModel> ValidateRecordView(Entity entity, InputRecordView recordView, bool checkId = false)
        {
            List<ErrorModel> errorList = new List<ErrorModel>();

            List<IStorageEntity> storageEntityList = EntityRepository.Read();
            List<Entity> entities = storageEntityList.MapTo<Entity>();

            EntityRelationManager relationManager = new EntityRelationManager(Storage);
            EntityRelationListResponse relationListResponse = relationManager.Read();
            List<EntityRelation> relationList = new List<EntityRelation>();
            if (relationListResponse.Object != null)
                relationList = relationListResponse.Object;

            if (!recordView.Id.HasValue || recordView.Id == Guid.Empty)
                errorList.Add(new ErrorModel("id", null, "Id is required!"));

            if (checkId)
            {
                int viewSameIdCount = entity.RecordLists.Where(f => f.Id == recordView.Id).Count();

                if (viewSameIdCount > 1)
                    errorList.Add(new ErrorModel("id", null, "There is already a view with such Id!"));

                int viewSameNameCount = entity.Fields.Where(f => f.Name == recordView.Name).Count();

                if (viewSameNameCount > 1)
                    errorList.Add(new ErrorModel("name", null, "There is already a view with such Name!"));
            }

            errorList.AddRange(ValidationUtility.ValidateViewName(recordView.Name));

            errorList.AddRange(ValidationUtility.ValidateLabel(recordView.Label));

            if (!recordView.Weight.HasValue)
                recordView.Weight = 1;

            if (!recordView.Default.HasValue)
                recordView.Default = false;

            if (!recordView.System.HasValue)
                recordView.System = false;

            if (string.IsNullOrWhiteSpace(recordView.Type))
                errorList.Add(new ErrorModel("type", null, "Type is required!"));
            else
            {
                RecordViewType type;
                if (!Enum.TryParse(recordView.Type, true, out type))
                    errorList.Add(new ErrorModel("type", recordView.Type, "Type is not valid!"));
            }

            if (recordView.Regions != null && recordView.Regions.Count > 0)
            {
                foreach (var region in recordView.Regions)
                {
                    if (string.IsNullOrWhiteSpace(region.Name))
                        errorList.Add(new ErrorModel("regions.name", region.Name, "Name is required!"));
                    else
                    {
                        if (recordView.Regions.Where(r => r.Name == region.Name).Count() > 1)
                            errorList.Add(new ErrorModel("regions.name", region.Name, "There is already region with such name!"));

                        errorList.AddRange(ValidationUtility.ValidateName(region.Name, key: "regions.name"));
                    }

                    if (!region.Render.HasValue)
                        region.Render = false;

                    if (region.Sections != null && region.Sections.Count > 0)
                    {
                        foreach (var section in region.Sections)
                        {
                            if (!section.Id.HasValue || section.Id == Guid.Empty)
                            {
                                errorList.Add(new ErrorModel("regions.sections.id", null, "Id is required!"));
                            }
                            else
                            {
                                if (region.Sections.Where(s => s.Id == section.Id).Count() > 1)
                                    errorList.Add(new ErrorModel("regions.sections.id", null, "There is already a section with such Id!"));
                            }

                            if (string.IsNullOrWhiteSpace(section.Name))
                            {
                                errorList.Add(new ErrorModel("regions.sections.name", region.Name, "Name is required!"));
                            }
                            else
                            {
                                if (region.Sections.Where(s => s.Name == section.Name).Count() > 1)
                                    errorList.Add(new ErrorModel("regions.sections.name", section.Name, "There is already section with such name!"));

                                errorList.AddRange(ValidationUtility.ValidateName(section.Name, key: "regions.sections.name"));
                            }

                            errorList.AddRange(ValidationUtility.ValidateLabel(section.Label, key: "regions.sections.label"));

                            if (!section.ShowLabel.HasValue)
                                section.ShowLabel = false;

                            if (!section.Collapsed.HasValue)
                                section.Collapsed = false;

                            if (!section.Weight.HasValue)
                                section.Weight = 1;

                            if (section.Rows != null && section.Rows.Count > 0)
                            {
                                foreach (var row in section.Rows)
                                {
                                    if (!row.Id.HasValue || row.Id == Guid.Empty)
                                    {
                                        errorList.Add(new ErrorModel("regions.sections.rows.id", null, "Id is required!"));
                                    }
                                    else
                                    {
                                        if (section.Rows.Where(r => r.Id == row.Id).Count() > 1)
                                            errorList.Add(new ErrorModel("regions.sections.rows.id", null, "There is already a row with such Id!"));
                                    }

                                    if (!row.Weight.HasValue)
                                        row.Weight = 1;

                                    if (row.Columns != null && row.Columns.Count > 0)
                                    {
                                        foreach (var column in row.Columns)
                                        {
                                            if (column.Items != null && column.Items.Count > 0)
                                            {
                                                foreach (var item in column.Items)
                                                {
                                                    if (item is InputRecordViewFieldItem)
                                                    {
                                                        InputRecordViewFieldItem inputItem = (InputRecordViewFieldItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.FieldName) && inputItem.FieldId == null)
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "Filed name and id are missing!"));
                                                        else
                                                        {
                                                            if (inputItem.FieldId == null)
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewFieldItem && ((InputRecordViewFieldItem)i).FieldName == inputItem.FieldName).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "There is already an item with such field name!"));

                                                                if (!entity.Fields.Any(f => f.Name == inputItem.FieldName))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "Wrong Name. There is no field with such name!"));
                                                                else
                                                                    inputItem.FieldId = entity.Fields.FirstOrDefault(f => f.Name == inputItem.FieldName).Id;
                                                            }
                                                            else if (string.IsNullOrWhiteSpace(inputItem.FieldName))
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewFieldItem && ((InputRecordViewFieldItem)i).FieldId == inputItem.FieldId).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldId", null, "There is already an item with such field id!"));

                                                                if (!entity.Fields.Any(f => f.Id == inputItem.FieldId))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldId", null, "Wrong Id. There is no field with such id!"));
                                                                else
                                                                    inputItem.FieldName = entity.Fields.FirstOrDefault(f => f.Id == inputItem.FieldId).Name;
                                                            }
                                                        }

                                                        inputItem.EntityId = entity.Id;
                                                        inputItem.EntityName = entity.Name;
                                                    }
                                                    else if (item is InputRecordViewListItem)
                                                    {
                                                        InputRecordViewListItem inputItem = (InputRecordViewListItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.ListName) && inputItem.ListId == null)
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "List name and id are missing!"));
                                                        else
                                                        {
                                                            if (inputItem.ListId == null)
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewListItem && ((InputRecordViewListItem)i).ListName == inputItem.ListName).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "There is already an item with such list name!"));

                                                                if (!entity.RecordLists.Any(l => l.Name == inputItem.ListName))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "Wrong name. There is no list with such name!"));
                                                                else
                                                                    inputItem.ListId = entity.RecordLists.FirstOrDefault(l => l.Name == inputItem.ListName).Id;
                                                            }
                                                            else if (string.IsNullOrWhiteSpace(inputItem.ListName))
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewListItem && ((InputRecordViewListItem)i).ListId == inputItem.ListId).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "There is already an item with such list id!"));

                                                                if (!entity.RecordLists.Any(l => l.Id == inputItem.ListId))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "Wrong id. There is no list with such id!"));
                                                                else

                                                                    inputItem.ListName = entity.RecordLists.FirstOrDefault(l => l.Id == inputItem.ListId).Name;
                                                            }
                                                        }

                                                        inputItem.EntityId = entity.Id;
                                                        inputItem.EntityName = entity.Name;
                                                    }
                                                    else if (item is InputRecordViewViewItem)
                                                    {
                                                        InputRecordViewViewItem inputItem = (InputRecordViewViewItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.ViewName) && inputItem.ViewId == null)
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "View name and id are missing!"));
                                                        else
                                                        {
                                                            if (inputItem.ViewId == null)
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewViewItem && ((InputRecordViewViewItem)i).ViewName == inputItem.ViewName).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "There is already an item with such view name!"));

                                                                if (!entity.RecordViews.Any(v => v.Name == inputItem.ViewName))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "Wrong name. There is no view with such name!"));
                                                                else
                                                                    inputItem.ViewId = entity.RecordViews.FirstOrDefault(v => v.Name == inputItem.ViewName).Id;
                                                            }
                                                            else if (string.IsNullOrWhiteSpace(inputItem.ViewName))
                                                            {
                                                                if (column.Items.Where(i => i is InputRecordViewViewItem && ((InputRecordViewViewItem)i).ViewId == inputItem.ViewId).Count() > 1)
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "There is already an item with such view id!"));

                                                                if (!entity.RecordViews.Any(v => v.Id == inputItem.ViewId))
                                                                    errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "Wrong id. There is no view with such id!"));
                                                                else
                                                                    inputItem.ViewName = entity.RecordViews.FirstOrDefault(v => v.Id == inputItem.ViewId).Name;
                                                            }
                                                        }

                                                        inputItem.EntityId = entity.Id;
                                                        inputItem.EntityName = entity.Name;
                                                    }
                                                    else if (item is InputRecordViewRelationFieldItem)
                                                    {
                                                        EntityRelation relation = null;

                                                        InputRecordViewRelationFieldItem inputItem = (InputRecordViewRelationFieldItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.RelationName) && inputItem.RelationId == null)
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Relation name or id is required!"));
                                                        }
                                                        else
                                                        {
                                                            if (inputItem.RelationId != null)
                                                                relation = relationList.FirstOrDefault(r => r.Id == inputItem.RelationId);
                                                            else
                                                                relation = relationList.FirstOrDefault(r => r.Name == inputItem.RelationName);

                                                            if (relation == null)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Wrong name or id. There is no relation with such name or id!"));
                                                            else
                                                            {
                                                                inputItem.RelationId = relation.Id;
                                                                inputItem.RelationName = relation.Name;
                                                            }
                                                        }

                                                        if (string.IsNullOrWhiteSpace(inputItem.FieldName) && inputItem.FieldId == null)
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "Field name or id is required!"));
                                                        }
                                                        else if (inputItem.RelationId.HasValue && inputItem.RelationId != Guid.Empty)
                                                        {

                                                            bool foundMoreThanOneTime = false;
                                                            if (inputItem.FieldId == null)
                                                                foundMoreThanOneTime = column.Items.Where(i => i is InputRecordViewRelationFieldItem
                                                                                    && ((InputRecordViewRelationFieldItem)i).FieldName == inputItem.FieldName
                                                                                    && ((InputRecordViewRelationFieldItem)i).RelationId == inputItem.RelationId ).Count() > 1;
                                                            else
                                                                foundMoreThanOneTime = column.Items.Where(i => i is InputRecordViewRelationFieldItem 
                                                                                && ((InputRecordViewRelationFieldItem)i).FieldId == inputItem.FieldId
                                                                                && ((InputRecordViewRelationFieldItem)i).RelationId == inputItem.RelationId ).Count() > 1;

                                                            if (foundMoreThanOneTime)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "There is already an item with such field name or id!"));


                                                            if (relation != null)
                                                            {
                                                                Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                                                Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                                                if (relEntity != null)
                                                                {
                                                                    inputItem.EntityId = entity.Id;
                                                                    inputItem.EntityName = entity.Name;


                                                                    Field relField = null;
                                                                    if( !string.IsNullOrWhiteSpace(inputItem.FieldName))
                                                                        relField = relEntity.Fields.FirstOrDefault(f => f.Name == inputItem.FieldName);
                                                                    else
                                                                        relField = relEntity.Fields.FirstOrDefault(f => f.Id == inputItem.FieldId);

                                                                    if (relField == null)
                                                                        errorList.Add(new ErrorModel("regions.sections.rows.columns.items.fieldName", null, "Wrong name. There is no field with such name!"));
                                                                    else
                                                                    {
                                                                        inputItem.FieldId = relField.Id;
                                                                        inputItem.FieldName = relField.Name;
                                                                    }
                                                                }
                                                            }
                                                        }

                                                    }
                                                    else if (item is InputRecordViewRelationListItem)
                                                    {
                                                        EntityRelation relation = null;
                                                        InputRecordViewRelationListItem inputItem = (InputRecordViewRelationListItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.RelationName) && inputItem.RelationId == null )
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Relation name or id is required!"));
                                                        }
                                                        else
                                                        {
                                                            if(string.IsNullOrWhiteSpace(inputItem.RelationName))
                                                                relation = relationList.SingleOrDefault(r => r.Id == inputItem.RelationId);
                                                            else
                                                                relation = relationList.SingleOrDefault(r => r.Name == inputItem.RelationName);

                                                            if (relation == null)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Wrong name or id. There is no relation with such name or id!"));
                                                            else
                                                            {
                                                                inputItem.RelationId = relation.Id;
                                                                inputItem.RelationName = relation.Name;
                                                            }
                                                        }

                                                        if (string.IsNullOrWhiteSpace(inputItem.ListName) && inputItem.ListId == null )
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "List name or id is required!"));
                                                        }
                                                        else if (inputItem.RelationId.HasValue && inputItem.RelationId != Guid.Empty)
                                                        {
                                                            InputRecordViewRelationListItem listItm = null;
                                                            if (string.IsNullOrWhiteSpace(inputItem.ListName))
                                                                listItm = (InputRecordViewRelationListItem)column.Items.Where(i => i is InputRecordViewRelationListItem && ((InputRecordViewRelationListItem)i).ListId == inputItem.ListId);
                                                            else
                                                                listItm = (InputRecordViewRelationListItem)column.Items.Where(i => i is InputRecordViewRelationListItem && ((InputRecordViewRelationListItem)i).ListName == inputItem.ListName);

                                                            if (listItm != null)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "There is already an item with such list name or id!"));

                                                            if (relation != null)
                                                            {
                                                                Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                                                Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                                                if (relEntity != null)
                                                                {
                                                                    inputItem.EntityId = entity.Id;
                                                                    inputItem.EntityName = entity.Name;

                                                                    RecordList relList = null;
                                                                    if( string.IsNullOrWhiteSpace(inputItem.ListName) )
                                                                        relList = relEntity.RecordLists.FirstOrDefault(l => l.Id == inputItem.ListId);
                                                                    else
                                                                        relList = relEntity.RecordLists.FirstOrDefault(l => l.Name == inputItem.ListName);

                                                                    if (relList != null)
                                                                    {
                                                                        inputItem.ListId = relList.Id;
                                                                        inputItem.ListName = relList.Name;
                                                                    }
                                                                    else
                                                                        errorList.Add(new ErrorModel("regions.sections.rows.columns.items.listName", null, "Wrong Name. There is no list with such name or id!"));
                                                                }
                                                            }
                                                        }

                                                    }
                                                    else if (item is InputRecordViewRelationViewItem)
                                                    {
                                                        EntityRelation relation = null;
                                                        InputRecordViewRelationViewItem inputItem = (InputRecordViewRelationViewItem)item;
                                                        if (string.IsNullOrWhiteSpace(inputItem.RelationName) && inputItem.RelationId == null )
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Relation name or id is required!"));
                                                        }
                                                        else
                                                        {
                                                            if (string.IsNullOrWhiteSpace(inputItem.RelationName))
                                                                relation = relationList.SingleOrDefault(r => r.Id == inputItem.RelationId);
                                                            else
                                                                relation = relationList.SingleOrDefault(r => r.Name == inputItem.RelationName);

                                                            if (relation == null)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.relationName", null, "Wrong name or. There is no relation with such name or id!"));
                                                            else
                                                                inputItem.RelationId = relationList.FirstOrDefault(r => r.Name == inputItem.RelationName).Id;
                                                        }

                                                        if (string.IsNullOrWhiteSpace(inputItem.ViewName))
                                                        {
                                                            errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "View name is required!"));
                                                        }
                                                        else if (inputItem.RelationId.HasValue && inputItem.RelationId != Guid.Empty)
                                                        {
                                                            if (relation != null)
                                                            {
                                                                Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                                                Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                                                if (relEntity != null)
                                                                {
                                                                    inputItem.EntityId = entity.Id;
                                                                    inputItem.EntityName = entity.Name;


                                                                    RecordView relView = null;
                                                                    if (string.IsNullOrWhiteSpace(inputItem.ViewName))
                                                                        relView = relEntity.RecordViews.FirstOrDefault(l => l.Id == inputItem.ViewId);
                                                                    else
                                                                        relView = relEntity.RecordViews.FirstOrDefault(l => l.Name == inputItem.ViewName);

                                                                    if (relView != null)
                                                                    {
                                                                        inputItem.ViewId = relView.Id;
                                                                        inputItem.ViewName = relView.Name;
                                                                    }
                                                                    else
                                                                        errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "Wrong name. There is no view with such name!"));
                                                                }
                                                            }

                                                            if (column.Items.Where(i => i is InputRecordViewRelationViewItem && ((InputRecordViewRelationViewItem)i).ViewName == inputItem.ViewName).Count() > 1)
                                                                errorList.Add(new ErrorModel("regions.sections.rows.columns.items.viewName", null, "There is already an item with such view name!"));
                                                        }

                                                    }
                                                    else if (item is InputRecordViewHtmlItem)
                                                    {
                                                        ((InputRecordViewHtmlItem)item).Tag = ((InputRecordViewHtmlItem)item).Tag.Trim();
                                                        ((InputRecordViewHtmlItem)item).Content = ((InputRecordViewHtmlItem)item).Content.Trim();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                        }
                    }
                }
            }

            if (recordView.Sidebar != null)
            {
                if (recordView.Sidebar.Items != null && recordView.Sidebar.Items.Count > 0)
                {
                    foreach (var item in recordView.Sidebar.Items)
                    {
                        if (item is InputRecordViewSidebarListItem)
                        {
                            InputRecordViewSidebarListItem inputItem = (InputRecordViewSidebarListItem)item;
                            if (string.IsNullOrWhiteSpace(inputItem.ListName) && inputItem.ListId == null )
                            {
                                errorList.Add(new ErrorModel("sidebar.items.listName", null, "List name or id is required!"));
                            }
                            else
                            {
                                RecordList list = null;
                                if (string.IsNullOrWhiteSpace(inputItem.ListName))
                                    list = entity.RecordLists.SingleOrDefault(l => l.Id == inputItem.ListId);
                                else
                                    list = entity.RecordLists.SingleOrDefault(l => l.Name == inputItem.ListName);


                                if (list == null )
                                    errorList.Add(new ErrorModel("sidebar.items.listName", null, "Wrong name. There is no list with such name or id!"));
                                else
                                {
                                    inputItem.ListId = list.Id;
                                    inputItem.ListName = list.Name;
                                }

                                if (recordView.Sidebar.Items.Where(i => i is InputRecordViewSidebarListItem && ((InputRecordViewSidebarListItem)i).ListName == inputItem.ListName).Count() > 1)
                                    errorList.Add(new ErrorModel("sidebar.items.listName", null, "There is already an item with such list name or id!"));
                            }
                        }
                        else if (item is InputRecordViewSidebarViewItem)
                        {
                            InputRecordViewSidebarViewItem inputItem = (InputRecordViewSidebarViewItem)item;
                            if (string.IsNullOrWhiteSpace(inputItem.ViewName) && inputItem.ViewId == null )
                            {
                                errorList.Add(new ErrorModel("sidebar.items.viewName", null, "View name or id is required!"));
                            }
                            else
                            {

                                RecordView view = null;
                                if (string.IsNullOrWhiteSpace(inputItem.ViewName))
                                    view = entity.RecordViews.SingleOrDefault(l => l.Id == inputItem.ViewId);
                                else
                                    view = entity.RecordViews.SingleOrDefault(l => l.Name == inputItem.ViewName);

                                if (view == null)
                                    errorList.Add(new ErrorModel("sidebar.items.viewName", null, "Wrong name. There is no view with such name or id!"));
                                else
                                {
                                    inputItem.ViewId = entity.RecordViews.FirstOrDefault(v => v.Name == inputItem.ViewName).Id;
                                }

                                if (recordView.Sidebar.Items.Where(i => i is InputRecordViewSidebarViewItem && ((InputRecordViewSidebarViewItem)i).ViewName == inputItem.ViewName).Count() > 1)
                                    errorList.Add(new ErrorModel("sidebar.items.viewName", null, "There is already an item with such view name or id!"));

                            }
                        }
                        else if (item is InputRecordViewSidebarRelationListItem)
                        {
                            EntityRelation relation = null;
                            InputRecordViewSidebarRelationListItem inputItem = (InputRecordViewSidebarRelationListItem)item;
                            if (string.IsNullOrWhiteSpace(inputItem.RelationName) && inputItem.RelationId == null )
                            {
                                errorList.Add(new ErrorModel("sidebar.items.relationName", null, "Relation name or id is required!"));
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(inputItem.RelationName))
                                    relation = relationList.SingleOrDefault(r => r.Id == inputItem.RelationId.Value);
                                else
                                    relation = relationList.SingleOrDefault(r => r.Name == inputItem.RelationName);

                                if (relation == null)
                                    errorList.Add(new ErrorModel("sidebar.items.relationName", null, "Wrong name. There is no relation with such name or id!"));
                                else
                                {
                                    inputItem.RelationId = relation.Id;
                                    inputItem.RelationName = relation.Name;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(inputItem.ListName) && inputItem.ListId == null )
                            {
                                errorList.Add(new ErrorModel("sidebar.items.listName", null, "List name or id is required!"));
                            }
                            else if (inputItem.RelationId.HasValue && inputItem.RelationId != Guid.Empty)
                            {
                                if (relation != null)
                                {
                                    Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                    Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                    inputItem.EntityId = relEntity.Id;
                                    inputItem.EntityName = relEntity.Name;

                                    if (relEntity != null)
                                    {
                                        RecordList relList = null;
                                        if (string.IsNullOrWhiteSpace(inputItem.ListName))
                                            relList = relEntity.RecordLists.FirstOrDefault(l => l.Id == inputItem.ListId);
                                        else
                                            relList = relEntity.RecordLists.FirstOrDefault(l => l.Name == inputItem.ListName);

                                        if (relList != null)
                                        {
                                            inputItem.ListId = relList.Id;
                                            inputItem.ListName = relList.Name;
                                        }
                                        else
                                            errorList.Add(new ErrorModel("sidebar.items.listName", null, "Wrong name. There is no list with such name or id!"));
                                    }
                                }

                                if (recordView.Sidebar.Items.Where(i => i is InputRecordViewSidebarRelationListItem &&
                                       ((InputRecordViewSidebarRelationListItem)i).ListName == inputItem.ListName &&
                                       ((InputRecordViewSidebarRelationListItem)i).RelationId == inputItem.RelationId ).Count() > 1)
                                    errorList.Add(new ErrorModel("sidebar.items.listName", null, "There is already an item with such list name!"));

                            }

                        }
                        else if (item is InputRecordViewSidebarRelationViewItem)
                        {
                            EntityRelation relation = null;
                            InputRecordViewSidebarRelationViewItem inputItem = (InputRecordViewSidebarRelationViewItem)item;
                            if (string.IsNullOrWhiteSpace(inputItem.RelationName) && inputItem.RelationId == null )
                            {
                                errorList.Add(new ErrorModel("sidebar.items.relationName", null, "Relation name or id is required!"));
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(inputItem.RelationName))
                                    relation = relationList.SingleOrDefault(r => r.Id == inputItem.RelationId.Value);
                                else
                                    relation = relationList.SingleOrDefault(r => r.Name == inputItem.RelationName);

                                if (relation == null)
                                    errorList.Add(new ErrorModel("sidebar.items.relationName", null, "Wrong name. There is no relation with such name or id!"));
                                else
                                {
                                    inputItem.RelationId = relation.Id;
                                    inputItem.RelationName = relation.Name;
                                }
                            }

                            if (string.IsNullOrWhiteSpace(inputItem.ViewName) && inputItem.ViewId == null)
                            {
                                errorList.Add(new ErrorModel("sidebar.items.viewName", null, "View name is required!"));
                            }
                            else if (inputItem.RelationId.HasValue && inputItem.RelationId != Guid.Empty)
                            {
                                if (relation != null)
                                {
                                    Guid relEntityId = entity.Id == relation.OriginEntityId ? relation.TargetEntityId : relation.OriginEntityId;
                                    Entity relEntity = entities.FirstOrDefault(e => e.Id == relEntityId);

                                    if (relEntity != null)
                                    {
                                        inputItem.EntityId = relEntity.Id;
                                        inputItem.EntityName = relEntity.Name;

                                        RecordView relView = null;
                                        if (string.IsNullOrWhiteSpace(inputItem.ViewName))
                                            relView = relEntity.RecordViews.FirstOrDefault(l => l.Id == inputItem.ViewId);
                                        else
                                            relView = relEntity.RecordViews.FirstOrDefault(l => l.Name == inputItem.ViewName);

                                        if (relView != null)
                                        {
                                            inputItem.ViewId = relView.Id;
                                            inputItem.ViewName = relView.Name;
                                        }
                                        else
                                            errorList.Add(new ErrorModel("sidebar.items.viewName", null, "Wrong name. There is no view with such name or id!"));
                                    }
                                }

                                if (recordView.Sidebar.Items.Where(i => i is InputRecordViewSidebarRelationViewItem && 
                                    ((InputRecordViewSidebarRelationViewItem)i).ViewName == inputItem.ViewName &&
                                    ((InputRecordViewSidebarRelationViewItem)i).RelationId == inputItem.RelationId ).Count() > 1)
                                    errorList.Add(new ErrorModel("sidebar.items.viewName", null, "There is already an item with such view name!"));

                            }

                        }
                    }
                }
            }

            return errorList;
        }

        #endregion

        #region << Entity methods >>

        public EntityResponse CreateEntity(InputEntity inputEntity)
        {
            EntityResponse response = new EntityResponse
            {
                Success = true,
                Message = "The entity was successfully created!",
            };

            //in order to support external IDs (while import in example)
            //we generate new ID only when it is not specified
            if (!inputEntity.Id.HasValue)
                inputEntity.Id = Guid.NewGuid();

            Entity entity = inputEntity.MapTo<Entity>();

            try
            {
                response.Object = entity;

                response.Errors = ValidateEntity(entity, false);

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The entity was not created. Validation error occurred!";
                    return response;
                }

                entity.Fields = CreateEntityDefaultFields(entity);
                entity.RecordLists = CreateEntityDefaultRecordLists(entity);
                entity.RecordViews = CreateEntityDefaultRecordViews(entity);

                IStorageEntity storageEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Create(storageEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The entity was not created! An internal error occurred!";
                    return response;
                }

                //TODO: create records collection

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = entity;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The entity was not created. An internal error occurred!";
#endif
                return response;
            }

            IStorageEntity createdEntity = EntityRepository.Read(entity.Id);
            response.Object = createdEntity.MapTo<Entity>();
            response.Timestamp = DateTime.UtcNow;

            return response;
        }

        public EntityResponse CreateEntity(Guid id, string name, string label, string labelPlural, List<Guid> allowedRolesRead = null,
            List<Guid> allowedRolesCreate = null, List<Guid> allowedRolesUpdate = null, List<Guid> allowedRolesDelete = null)
        {
            InputEntity entity = new InputEntity();
            entity.Id = id;
            entity.Name = name;
            entity.Label = label;
            entity.LabelPlural = labelPlural;
            entity.System = false;
            entity.RecordPermissions = new RecordPermissions();
            entity.RecordPermissions.CanRead = allowedRolesRead ?? new List<Guid>() { SystemIds.AdministratorRoleId };
            entity.RecordPermissions.CanCreate = allowedRolesCreate ?? new List<Guid>() { SystemIds.AdministratorRoleId };
            entity.RecordPermissions.CanUpdate = allowedRolesUpdate ?? new List<Guid>() { SystemIds.AdministratorRoleId };
            entity.RecordPermissions.CanDelete = allowedRolesDelete ?? new List<Guid>() { SystemIds.AdministratorRoleId };

            return CreateEntity(entity);
        }

        public EntityResponse UpdateEntity(InputEntity inputEntity)
        {
            EntityResponse response = new EntityResponse
            {
                Success = true,
                Message = "The entity was successfully updated!",
            };

            Entity entity = inputEntity.MapTo<Entity>();

            try
            {
                response.Object = entity;
                response.Errors = ValidateEntity(entity, true);

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The entity was not updated. Validation error occurred!";
                    return response;
                }

                IStorageEntity storageEntity = EntityRepository.Read(entity.Id);

                storageEntity.Label = entity.Label;
                storageEntity.LabelPlural = entity.LabelPlural;
                storageEntity.System = entity.System;
                storageEntity.IconName = entity.IconName;
                storageEntity.Weight = entity.Weight;
                storageEntity.RecordPermissions.CanRead = entity.RecordPermissions.CanRead;
                storageEntity.RecordPermissions.CanCreate = entity.RecordPermissions.CanCreate;
                storageEntity.RecordPermissions.CanUpdate = entity.RecordPermissions.CanUpdate;
                storageEntity.RecordPermissions.CanDelete = entity.RecordPermissions.CanDelete;

                bool result = EntityRepository.Update(storageEntity);

                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The entity was not updated! An internal error occurred!";
                    return response;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = entity;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The entity was not updated. An internal error occurred!";
#endif
                return response;
            }

            IStorageEntity updatedEntity = EntityRepository.Read(entity.Id);
            response.Object = updatedEntity.MapTo<Entity>();
            response.Timestamp = DateTime.UtcNow;

            return response;
        }

        //		public EntityResponse PartialUpdateEntity(Guid id, InputEntity inputEntity)
        //		{
        //			EntityResponse response = new EntityResponse
        //			{
        //				Success = true,
        //				Message = "The entity was successfully updated!",
        //			};

        //			Entity entity = null;

        //			try
        //			{
        //				IStorageEntity storageEntity = EntityRepository.Read(id);
        //				entity = storageEntity.MapTo<Entity>();

        //				if (inputEntity.Label != null)
        //					entity.Label = inputEntity.Label;
        //				if (inputEntity.LabelPlural != null)
        //					entity.LabelPlural = inputEntity.LabelPlural;
        //				if (inputEntity.System != null)
        //					entity.System = inputEntity.System.Value;
        //				if (inputEntity.IconName != null)
        //					entity.IconName = inputEntity.IconName;
        //				if (inputEntity.Weight != null)
        //					entity.Weight = inputEntity.Weight.Value;
        //				if (inputEntity.RecordPermissions != null)
        //					entity.RecordPermissions = inputEntity.RecordPermissions;

        //				response.Object = entity;
        //				response.Errors = ValidateEntity(entity, true);

        //				if (response.Errors.Count > 0)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The entity was not updated. Validation error occurred!";
        //					return response;
        //				}

        //				storageEntity = entity.MapTo<IStorageEntity>();

        //				bool result = EntityRepository.Update(storageEntity);

        //				if (!result)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The entity was not updated! An internal error occurred!";
        //					return response;
        //				}

        //			}
        //			catch (Exception e)
        //			{
        //				response.Success = false;
        //				response.Object = entity;
        //				response.Timestamp = DateTime.UtcNow;
        //#if DEBUG
        //				response.Message = e.Message + e.StackTrace;
        //#else
        //                response.Message = "The entity was not updated. An internal error occurred!";
        //#endif
        //				return response;
        //			}

        //			IStorageEntity updatedEntity = EntityRepository.Read(entity.Id);
        //			response.Object = updatedEntity.MapTo<Entity>();
        //			response.Timestamp = DateTime.UtcNow;

        //			return response;
        //		}

        public EntityResponse DeleteEntity(Guid id)
        {
            EntityResponse response = new EntityResponse
            {
                Success = true,
                Message = "The entity was successfully deleted!",
            };

            try
            {
                IStorageEntity entity = EntityRepository.Read(id);

                if (entity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The entity was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("id", id.ToString(), "Entity with such Id does not exist!"));
                    return response;
                }

                //entity, entity records and relations are deleted in storage repository 
                EntityRepository.Delete(id);
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The entity was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public EntityListResponse ReadEntities()
        {
            EntityListResponse response = new EntityListResponse
            {
                Success = true,
                Message = "The entity was successfully returned!",
            };

            try
            {
                List<IStorageEntity> storageEntityList = EntityRepository.Read();
                List<Entity> entities = storageEntityList.MapTo<Entity>();

                EntityRelationManager relationManager = new EntityRelationManager(Storage);
                EntityRelationListResponse relationListResponse = relationManager.Read();
                List<EntityRelation> relationList = new List<EntityRelation>();
                if (relationListResponse.Object != null)
                    relationList = relationListResponse.Object;

                List<RecordList> recordLists = new List<RecordList>();
                List<RecordView> recordViews = new List<RecordView>();
                List<Field> fields = new List<Field>();

                foreach (var entity in entities)
                {
                    recordLists.AddRange(entity.RecordLists);
                    recordViews.AddRange(entity.RecordViews);
                    fields.AddRange(entity.Fields);
                }

                foreach (var entity in entities)
                {
                    if (entity.RecordLists != null)
                    {
                        foreach (var recordList in entity.RecordLists)
                        {
                            if (recordList.Columns != null)
                            {
                                foreach (var column in recordList.Columns)
                                {
                                    if (column is RecordListFieldItem)
                                    {
                                        Field field = fields.FirstOrDefault(f => f.Id == ((RecordListFieldItem)column).FieldId);
                                        if (field != null)
                                        {
                                            //((RecordListFieldItem)column).DataName = string.Format("$field${0}", field.Name);
                                            ((RecordListFieldItem)column).DataName = field.Name;
                                            ((RecordListFieldItem)column).FieldName = field.Name;
                                            ((RecordListFieldItem)column).Meta = field;

                                            ((RecordListFieldItem)column).EntityName = entity.Name;
                                            ((RecordListFieldItem)column).EntityLabel = entity.Label;
                                            ((RecordListFieldItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }
                                    }
                                    if (column is RecordListRelationFieldItem)
                                    {
                                        Entity relEntity = GetEntityByFieldId(((RecordListRelationFieldItem)column).FieldId, entities);
                                        if (relEntity != null)
                                        {
                                            ((RecordListRelationFieldItem)column).EntityName = relEntity.Name;
                                            ((RecordListRelationFieldItem)column).EntityLabel = relEntity.Label;
                                            ((RecordListRelationFieldItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }

                                        var relation = relationList.FirstOrDefault(r => r.Id == ((RecordListRelationFieldItem)column).RelationId);
                                        ((RecordListRelationFieldItem)column).RelationName = relation != null ? relation.Name : string.Empty;

                                        Field field = fields.FirstOrDefault(f => f.Id == ((RecordListRelationFieldItem)column).FieldId);
                                        if (field != null)
                                        {
                                            ((RecordListRelationFieldItem)column).DataName = string.Format("$field${0}${1}", ((RecordListRelationFieldItem)column).RelationName, field.Name);
                                            ((RecordListRelationFieldItem)column).FieldName = field.Name;
                                            ((RecordListRelationFieldItem)column).Meta = field;
                                        }
                                    }
                                    if (column is RecordListViewItem)
                                    {
                                        RecordView view = recordViews.FirstOrDefault(v => v.Id == ((RecordListViewItem)column).ViewId);
                                        if (view != null)
                                        {
                                            ((RecordListViewItem)column).DataName = string.Format("$view${0}", view.Name);
                                            ((RecordListViewItem)column).ViewName = view.Name;
                                            ((RecordListViewItem)column).Meta = view;

                                            ((RecordListViewItem)column).EntityName = entity.Name;
                                            ((RecordListViewItem)column).EntityLabel = entity.Label;
                                            ((RecordListViewItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }
                                    }
                                    if (column is RecordListRelationViewItem)
                                    {
                                        Entity relEntity = GetEntityByViewId(((RecordListRelationViewItem)column).ViewId, entities);
                                        if (relEntity != null)
                                        {
                                            ((RecordListRelationViewItem)column).EntityName = relEntity.Name;
                                            ((RecordListRelationViewItem)column).EntityLabel = relEntity.Label;
                                            ((RecordListRelationViewItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }

                                        var relation = relationList.FirstOrDefault(r => r.Id == ((RecordListRelationViewItem)column).RelationId);
                                        ((RecordListRelationViewItem)column).RelationName = relation != null ? relation.Name : string.Empty;

                                        RecordView view = recordViews.FirstOrDefault(v => v.Id == ((RecordListRelationViewItem)column).ViewId);
                                        if (view != null)
                                        {
                                            ((RecordListRelationViewItem)column).DataName = string.Format("$view${0}${1}", ((RecordListRelationViewItem)column).RelationName, view.Name);
                                            ((RecordListRelationViewItem)column).ViewName = view.Name;
                                            ((RecordListRelationViewItem)column).Meta = view;
                                        }
                                    }
                                    if (column is RecordListListItem)
                                    {
                                        RecordList list = recordLists.FirstOrDefault(l => l.Id == ((RecordListListItem)column).ListId);
                                        if (list != null)
                                        {
                                            ((RecordListListItem)column).DataName = string.Format("list${0}", list.Name);
                                            ((RecordListListItem)column).ListName = list.Name;
                                            ((RecordListListItem)column).Meta = list;

                                            ((RecordListListItem)column).EntityName = entity.Name;
                                            ((RecordListListItem)column).EntityLabel = entity.Label;
                                            ((RecordListListItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }
                                    }
                                    if (column is RecordListRelationListItem)
                                    {
                                        Entity relEntity = GetEntityByListId(((RecordListRelationListItem)column).ListId, entities);
                                        if (relEntity != null)
                                        {
                                            ((RecordListRelationListItem)column).EntityName = relEntity.Name;
                                            ((RecordListRelationListItem)column).EntityLabel = relEntity.Label;
                                            ((RecordListRelationListItem)column).EntityLabelPlural = entity.LabelPlural;
                                        }

                                        var relation = relationList.FirstOrDefault(r => r.Id == ((RecordListRelationListItem)column).RelationId);
                                        ((RecordListRelationListItem)column).RelationName = relation != null ? relation.Name : string.Empty;

                                        RecordList list = recordLists.FirstOrDefault(l => l.Id == ((RecordListRelationListItem)column).ListId);
                                        if (list != null)
                                        {
                                            ((RecordListRelationListItem)column).DataName = string.Format("$list${0}${1}", ((RecordListRelationListItem)column).RelationName, list.Name);
                                            ((RecordListRelationListItem)column).ListName = list.Name;
                                            ((RecordListRelationListItem)column).Meta = list;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (entity.RecordViews != null)
                    {
                        foreach (var recordView in entity.RecordViews)
                        {
                            if (recordView.Regions == null)
                                continue;

                            foreach (var region in recordView.Regions)
                            {
                                if (region.Sections == null)
                                    continue;

                                foreach (var section in region.Sections)
                                {
                                    if (section.Rows == null)
                                        continue;

                                    foreach (var row in section.Rows)
                                    {
                                        if (row.Columns == null)
                                            continue;

                                        foreach (var column in row.Columns)
                                        {
                                            if (column.Items == null)
                                                continue;

                                            foreach (var item in column.Items)
                                            {
                                                if (item is RecordViewFieldItem)
                                                {
                                                    Field field = fields.FirstOrDefault(f => f.Id == ((RecordViewFieldItem)item).FieldId);
                                                    if (field != null)
                                                    {
                                                        //((RecordViewFieldItem)item).DataName = string.Format("$field${0}", field.Name);
                                                        ((RecordViewFieldItem)item).DataName = field.Name;
                                                        ((RecordViewFieldItem)item).FieldName = field.Name;
                                                        ((RecordViewFieldItem)item).Meta = field;

                                                        ((RecordViewFieldItem)item).EntityId = entity.Id;
                                                        ((RecordViewFieldItem)item).EntityName = entity.Name;
                                                        ((RecordViewFieldItem)item).EntityLabel = entity.Label;
                                                        ((RecordViewFieldItem)item).EntityLabelPlural = entity.LabelPlural;
                                                    }
                                                }
                                                if (item is RecordViewListItem)
                                                {
                                                    RecordList list = entity.RecordLists.FirstOrDefault(l => l.Id == ((RecordViewListItem)item).ListId);
                                                    if (list != null)
                                                    {
                                                        ((RecordViewListItem)item).DataName = string.Format("$list${0}", list.Name);
                                                        ((RecordViewListItem)item).Meta = list;
                                                        ((RecordViewListItem)item).ListName = list.Name;

                                                        ((RecordViewListItem)item).EntityId = entity.Id;
                                                        ((RecordViewListItem)item).EntityName = entity.Name;
                                                        ((RecordViewListItem)item).EntityLabel = entity.Label;
                                                        ((RecordViewListItem)item).EntityLabelPlural = entity.LabelPlural;
                                                    }

                                                }
                                                if (item is RecordViewViewItem)
                                                {
                                                    RecordView recView = entity.RecordViews.FirstOrDefault(v => v.Id == ((RecordViewViewItem)item).ViewId);
                                                    if (recView != null)
                                                    {
                                                        ((RecordViewViewItem)item).DataName = string.Format("$view${0}", recView.Name);
                                                        ((RecordViewViewItem)item).Meta = recView;
                                                        ((RecordViewViewItem)item).ViewName = recView.Name;

                                                        ((RecordViewViewItem)item).EntityId = entity.Id;
                                                        ((RecordViewViewItem)item).EntityName = entity.Name;
                                                        ((RecordViewViewItem)item).EntityLabel = entity.Label;
                                                        ((RecordViewViewItem)item).EntityLabelPlural = entity.LabelPlural;
                                                    }
                                                }

                                                if (item is RecordViewRelationFieldItem)
                                                {
                                                    Entity relEntity = GetEntityByFieldId(((RecordViewRelationFieldItem)item).FieldId, entities);
                                                    if (relEntity != null)
                                                    {
                                                        ((RecordViewRelationFieldItem)item).EntityId = relEntity.Id;
                                                        ((RecordViewRelationFieldItem)item).EntityName = relEntity.Name;
                                                        ((RecordViewRelationFieldItem)item).EntityLabel = relEntity.Label;
                                                    }

                                                    var relation = relationList.FirstOrDefault(r => r.Id == ((RecordViewRelationFieldItem)item).RelationId);
                                                    ((RecordViewRelationFieldItem)item).RelationName = relation != null ? relation.Name : string.Empty;

                                                    Field field = fields.FirstOrDefault(f => f.Id == ((RecordViewRelationFieldItem)item).FieldId);
                                                    if (field != null)
                                                    {
                                                        ((RecordViewRelationFieldItem)item).DataName = string.Format("$field${0}${1}", ((RecordViewRelationFieldItem)item).RelationName, field.Name);
                                                        ((RecordViewRelationFieldItem)item).Meta = field;
                                                        ((RecordViewRelationFieldItem)item).FieldName = field.Name;
                                                    }
                                                }

                                                if (item is RecordViewRelationViewItem)
                                                {
                                                    var relation = relationList.FirstOrDefault(r => r.Id == ((RecordViewRelationViewItem)item).RelationId);
                                                    ((RecordViewRelationViewItem)item).RelationName = relation != null ? relation.Name : string.Empty;

                                                    Entity relEntity = GetEntityByViewId(((RecordViewRelationViewItem)item).ViewId, entities);
                                                    if (relEntity != null)
                                                    {
                                                        ((RecordViewRelationViewItem)item).EntityId = relEntity.Id;
                                                        ((RecordViewRelationViewItem)item).EntityName = relEntity.Name;
                                                        ((RecordViewRelationViewItem)item).EntityLabel = relEntity.Label;

                                                        RecordView view = relEntity.RecordViews.FirstOrDefault(f => f.Id == ((RecordViewRelationViewItem)item).ViewId);
                                                        if (view != null)
                                                        {
                                                            ((RecordViewRelationViewItem)item).DataName = string.Format("$view${0}${1}", ((RecordViewRelationViewItem)item).RelationName, view.Name);
                                                            ((RecordViewRelationViewItem)item).Meta = view;
                                                            ((RecordViewRelationViewItem)item).ViewName = view.Name;
                                                        }
                                                    }
                                                }

                                                if (item is RecordViewRelationListItem)
                                                {
                                                    var relation = relationList.FirstOrDefault(r => r.Id == ((RecordViewRelationListItem)item).RelationId);
                                                    ((RecordViewRelationListItem)item).RelationName = relation != null ? relation.Name : string.Empty;

                                                    Entity relEntity = GetEntityByListId(((RecordViewRelationListItem)item).ListId, entities);
                                                    if (relEntity != null)
                                                    {
                                                        ((RecordViewRelationListItem)item).EntityId = relEntity.Id;
                                                        ((RecordViewRelationListItem)item).EntityName = relEntity.Name;
                                                        ((RecordViewRelationListItem)item).EntityLabel = relEntity.Label;
                                                        ((RecordViewRelationListItem)item).EntityLabelPlural = relEntity.LabelPlural;

                                                        RecordList list = relEntity.RecordLists.FirstOrDefault(f => f.Id == ((RecordViewRelationListItem)item).ListId);
                                                        if (list != null)
                                                        {
                                                            ((RecordViewRelationListItem)item).DataName = string.Format("$list${0}${1}", ((RecordViewRelationListItem)item).RelationName, list.Name);
                                                            ((RecordViewRelationListItem)item).Meta = list;
                                                            ((RecordViewRelationListItem)item).ListName = list.Name;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (recordView.Sidebar != null)
                            {
                                foreach (var item in recordView.Sidebar.Items)
                                {
                                    if (item is RecordViewSidebarListItem)
                                    {
                                        RecordList list = entity.RecordLists.FirstOrDefault(l => l.Id == ((RecordViewSidebarListItem)item).ListId);
                                        if (list != null)
                                        {
                                            ((RecordViewSidebarListItem)item).DataName = string.Format("$list${0}", list.Name);
                                            ((RecordViewSidebarListItem)item).Meta = list;
                                            ((RecordViewSidebarListItem)item).ListName = list.Name;

                                            ((RecordViewSidebarListItem)item).EntityId = entity.Id;
                                            ((RecordViewSidebarListItem)item).EntityName = entity.Name;
                                            ((RecordViewSidebarListItem)item).EntityLabel = entity.Label;
                                            ((RecordViewSidebarListItem)item).EntityLabelPlural = entity.LabelPlural;
                                        }

                                    }
                                    if (item is RecordViewSidebarViewItem)
                                    {
                                        RecordView recView = entity.RecordViews.FirstOrDefault(v => v.Id == ((RecordViewSidebarViewItem)item).ViewId);
                                        if (recView != null)
                                        {
                                            ((RecordViewSidebarViewItem)item).DataName = string.Format("$view${0}", recView.Name);
                                            ((RecordViewSidebarViewItem)item).Meta = recView;
                                            ((RecordViewSidebarViewItem)item).ViewName = recView.Name;

                                            ((RecordViewSidebarViewItem)item).EntityId = entity.Id;
                                            ((RecordViewSidebarViewItem)item).EntityName = entity.Name;
                                            ((RecordViewSidebarViewItem)item).EntityLabel = entity.Label;
                                            ((RecordViewSidebarViewItem)item).EntityLabelPlural = entity.LabelPlural;
                                        }
                                    }
                                    if (item is RecordViewSidebarRelationViewItem)
                                    {
                                        var relation = relationList.FirstOrDefault(r => r.Id == ((RecordViewSidebarRelationViewItem)item).RelationId);
                                        ((RecordViewSidebarRelationViewItem)item).RelationName = relation != null ? relation.Name : string.Empty;

                                        Entity relEntity = GetEntityByViewId(((RecordViewSidebarRelationViewItem)item).ViewId, entities);
                                        if (relEntity != null)
                                        {
                                            ((RecordViewSidebarRelationViewItem)item).EntityId = relEntity.Id;
                                            ((RecordViewSidebarRelationViewItem)item).EntityName = relEntity.Name;
                                            ((RecordViewSidebarRelationViewItem)item).EntityLabel = relEntity.Label;

                                            RecordView view = relEntity.RecordViews.FirstOrDefault(f => f.Id == ((RecordViewSidebarRelationViewItem)item).ViewId);
                                            if (view != null)
                                            {
                                                ((RecordViewSidebarRelationViewItem)item).DataName = string.Format("$view${0}${1}", ((RecordViewSidebarRelationViewItem)item).RelationName, view.Name);
                                                ((RecordViewSidebarRelationViewItem)item).Meta = view;
                                                ((RecordViewSidebarRelationViewItem)item).ViewName = view.Name;
                                            }
                                        }
                                    }

                                    if (item is RecordViewSidebarRelationListItem)
                                    {
                                        var relation = relationList.FirstOrDefault(r => r.Id == ((RecordViewSidebarRelationListItem)item).RelationId);
                                        ((RecordViewSidebarRelationListItem)item).RelationName = relation != null ? relation.Name : string.Empty;

                                        Entity relEntity = GetEntityByListId(((RecordViewSidebarRelationListItem)item).ListId, entities);
                                        if (relEntity != null)
                                        {
                                            ((RecordViewSidebarRelationListItem)item).EntityId = relEntity.Id;
                                            ((RecordViewSidebarRelationListItem)item).EntityName = relEntity.Name;
                                            ((RecordViewSidebarRelationListItem)item).EntityLabel = relEntity.Label;
                                            ((RecordViewSidebarRelationListItem)item).EntityLabelPlural = relEntity.LabelPlural;

                                            RecordList list = relEntity.RecordLists.FirstOrDefault(f => f.Id == ((RecordViewSidebarRelationListItem)item).ListId);
                                            if (list != null)
                                            {
                                                ((RecordViewSidebarRelationListItem)item).DataName = string.Format("$list${0}${1}", ((RecordViewSidebarRelationListItem)item).RelationName, list.Name);
                                                ((RecordViewSidebarRelationListItem)item).Meta = list;
                                                ((RecordViewSidebarRelationListItem)item).ListName = list.Name;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                EntityList entityList = new EntityList();
                entityList.Entities = entities;
                response.Object = entityList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public EntityResponse ReadEntity(Guid id)
        {
            EntityResponse response = new EntityResponse
            {
                Success = true,
                Message = "The entity was successfully returned!",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                EntityListResponse entityListResponse = ReadEntities();

                if (entityListResponse != null && entityListResponse.Object != null)
                {
                    List<Entity> entities = entityListResponse.Object.Entities;

                    Entity entity = entities.FirstOrDefault(e => e.Id == id);
                    if (entity != null)
                        response.Object = entity;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public EntityResponse ReadEntity(string name)
        {
            EntityResponse response = new EntityResponse
            {
                Success = true,
                Message = "The entity was successfully returned!",
                Timestamp = DateTime.UtcNow
            };

            try
            {
                EntityListResponse entityListResponse = ReadEntities();

                if (entityListResponse != null && entityListResponse.Object != null)
                {
                    List<Entity> entities = entityListResponse.Object.Entities;

                    Entity entity = entities.FirstOrDefault(e => e.Name == name);
                    if (entity != null)
                        response.Object = entity;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        #endregion

        #region << Field methods >>

        public FieldResponse CreateField(Guid entityId, InputField inputField, bool transactional = true)
        {
            FieldResponse response = new FieldResponse
            {
                Success = true,
                Message = "The field was successfully created!",
            };

            Field field = null;

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                if (inputField.Id == null || inputField.Id == Guid.Empty)
                    inputField.Id = Guid.NewGuid();

                Entity entity = storageEntity.MapTo<Entity>();

                response.Errors = ValidateField(entity, inputField, false);

                field = inputField.MapTo<Field>();

                if (response.Errors.Count > 0)
                {
                    response.Object = field;
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The field was not created. Validation error occurred!";
                    return response;
                }

                entity.Fields.Add(field);

                IStorageEntity editedEntity = entity.MapTo<IStorageEntity>();

                var recRep = Storage.GetRecordRepository();
                var transaction = recRep.CreateTransaction();
                try
                {
                    if (transactional)
                        transaction.Begin();

                    recRep.CreateRecordField(entity.Name, field.Name, field.GetDefaultValue());


                    bool result = EntityRepository.Update(editedEntity);
                    if (!result)
                    {
                        response.Timestamp = DateTime.UtcNow;
                        response.Success = false;
                        response.Message = "The field was not created! An internal error occurred!";
                        return response;
                    }

                    if (transactional)
                        transaction.Commit();
                }
                catch
                {
                    if (transactional)
                        transaction.Rollback();
                    throw;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = field;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The field was not created. An internal error occurred!";
#endif
                return response;
            }

            response.Object = field;
            response.Timestamp = DateTime.UtcNow;

            return response;
        }

        public FieldResponse CreateField(Guid entityId, FieldType type, Expando data, string name, string label, Guid? id = null,
                    string placeholderText = "", string helpText = "", string description = "",
                    bool system = false, bool required = false, bool unique = false, bool searchable = false, bool auditable = false)
        {
            Field field = null;

            if (data == null)
                data = new Expando();

            switch (type)
            {
                case FieldType.AutoNumberField:
                    field = new AutoNumberField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((AutoNumberField)field).DefaultValue = (decimal?)data["defaultValue"];
                    if (HasKey(data, "startingNumber") && data["startingNumber"] != null)
                        ((AutoNumberField)field).StartingNumber = (decimal?)data["startingNumber"];
                    if (HasKey(data, "displayFormat") && data["displayFormat"] != null)
                        ((AutoNumberField)field).DisplayFormat = (string)data["displayFormat"];
                    break;
                case FieldType.CheckboxField:
                    field = new CheckboxField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((CheckboxField)field).DefaultValue = (bool?)data["defaultValue"] ?? false;
                    break;
                case FieldType.CurrencyField:
                    field = new CurrencyField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((CurrencyField)field).DefaultValue = (decimal?)data["defaultValue"];
                    if (HasKey(data, "minValue") && data["minValue"] != null)
                        ((CurrencyField)field).MinValue = (decimal?)data["minValue"];
                    if (HasKey(data, "maxValue") && data["maxValue"] != null)
                        ((CurrencyField)field).MaxValue = (decimal?)data["maxValue"];
                    if (HasKey(data, "currency") && data["currency"] != null)
                    {
                        ((CurrencyField)field).Currency = (CurrencyType)data["currency"];
                    }
                    else
                    {
                        ((CurrencyField)field).Currency = new CurrencyType();
                        ((CurrencyField)field).Currency.Code = "USD";
                        ((CurrencyField)field).Currency.DecimalDigits = 2;
                        ((CurrencyField)field).Currency.Name = "US dollar";
                        ((CurrencyField)field).Currency.NamePlural = "US dollars";
                        ((CurrencyField)field).Currency.Rounding = 0;
                        ((CurrencyField)field).Currency.Symbol = "$";
                        ((CurrencyField)field).Currency.SymbolNative = "$";
                        ((CurrencyField)field).Currency.SymbolPlacement = CurrencySymbolPlacement.Before;
                        ((CurrencyField)field).DefaultValue = 1;
                    }
                    break;
                case FieldType.DateField:
                    field = new DateField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((DateField)field).DefaultValue = (DateTime?)data["defaultValue"];
                    if (HasKey(data, "format") && data["format"] != null)
                        ((DateField)field).Format = (string)data["format"];
                    if (HasKey(data, "useCurrentTimeAsDefaultValue") && data["useCurrentTimeAsDefaultValue"] != null)
                        ((DateField)field).UseCurrentTimeAsDefaultValue = (bool?)data["useCurrentTimeAsDefaultValue"];
                    break;
                case FieldType.DateTimeField:
                    field = new DateTimeField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((DateTimeField)field).DefaultValue = (DateTime?)data["defaultValue"];
                    if (HasKey(data, "format") && data["format"] != null)
                        ((DateTimeField)field).Format = (string)data["format"];
                    if (HasKey(data, "useCurrentTimeAsDefaultValue") && data["useCurrentTimeAsDefaultValue"] != null)
                        ((DateTimeField)field).UseCurrentTimeAsDefaultValue = (bool?)data["useCurrentTimeAsDefaultValue"];
                    break;
                case FieldType.EmailField:
                    field = new EmailField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((EmailField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((EmailField)field).MaxLength = (int?)data["maxLength"];
                    break;
                case FieldType.FileField:
                    field = new FileField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((FileField)field).DefaultValue = (string)data["defaultValue"];
                    break;
                case FieldType.GuidField:
                    field = new GuidField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((GuidField)field).DefaultValue = (Guid?)data["defaultValue"];
                    if (HasKey(data, "generateNewId") && data["generateNewId"] != null)
                        ((GuidField)field).GenerateNewId = (bool?)data["generateNewId"];
                    break;
                case FieldType.HtmlField:
                    field = new HtmlField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((HtmlField)field).DefaultValue = (string)data["defaultValue"];
                    break;
                case FieldType.ImageField:
                    field = new ImageField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((ImageField)field).DefaultValue = (string)data["defaultValue"];
                    break;
                case FieldType.MultiLineTextField:
                    field = new MultiLineTextField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((MultiLineTextField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((MultiLineTextField)field).MaxLength = (int?)data["maxLength"];
                    if (HasKey(data, "visibleLineNumber") && data["visibleLineNumber"] != null)
                        ((MultiLineTextField)field).VisibleLineNumber = (int?)data["visibleLineNumber"];
                    break;
                case FieldType.MultiSelectField:
                    field = new MultiSelectField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((MultiSelectField)field).DefaultValue = (IEnumerable<string>)data["defaultValue"];
                    if (HasKey(data, "options") && data["options"] != null)
                        ((MultiSelectField)field).Options = (List<MultiSelectFieldOption>)data["options"];
                    break;
                case FieldType.NumberField:
                    field = new NumberField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((NumberField)field).DefaultValue = (int?)data["defaultValue"];
                    if (HasKey(data, "minValue") && data["minValue"] != null)
                        ((NumberField)field).MinValue = (decimal?)data["minValue"];
                    if (HasKey(data, "maxValue") && data["maxValue"] != null)
                        ((NumberField)field).MaxValue = (decimal?)data["maxValue"];
                    if (HasKey(data, "decimalPlaces") && data["decimalPlaces"] != null)
                        ((NumberField)field).DecimalPlaces = (byte?)data["decimalPlaces"];
                    break;
                case FieldType.PasswordField:
                    field = new PasswordField();
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((PasswordField)field).MaxLength = (int?)data["maxLength"];
                    if (HasKey(data, "minLength") && data["minLength"] != null)
                        ((PasswordField)field).MinLength = (int?)data["minLength"];
                    if (HasKey(data, "encrypted") && data["encrypted"] != null)
                        ((PasswordField)field).Encrypted = (bool?)data["encrypted"];
                    break;
                case FieldType.PercentField:
                    field = new PercentField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((PercentField)field).DefaultValue = (decimal?)data["defaultValue"]; //0.01m;
                    if (HasKey(data, "minValue") && data["minValue"] != null)
                        ((PercentField)field).MinValue = (decimal?)data["minValue"];
                    if (HasKey(data, "maxValue") && data["maxValue"] != null)
                        ((PercentField)field).MaxValue = (decimal?)data["maxValue"];
                    if (HasKey(data, "decimalPlaces") && data["decimalPlaces"] != null)
                        ((PercentField)field).DecimalPlaces = (byte?)data["decimalPlaces"];
                    break;
                case FieldType.PhoneField:
                    field = new PhoneField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((PhoneField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "format") && data["format"] != null)
                        ((PhoneField)field).Format = (string)data["format"];
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((PhoneField)field).DefaultValue = (string)data["maxLength"];
                    break;
                case FieldType.SelectField:
                    field = new SelectField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((SelectField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "options") && data["options"] != null)
                        ((SelectField)field).Options = (List<SelectFieldOption>)data["options"];
                    break;
                case FieldType.TextField:
                    field = new TextField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((TextField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((TextField)field).MaxLength = (int?)data["maxLength"];
                    break;
                case FieldType.UrlField:
                    field = new UrlField();
                    if (HasKey(data, "defaultValue") && data["defaultValue"] != null)
                        ((UrlField)field).DefaultValue = (string)data["defaultValue"];
                    if (HasKey(data, "maxLength") && data["maxLength"] != null)
                        ((UrlField)field).MaxLength = (int?)data["maxLength"];
                    if (HasKey(data, "openTargetInNewWindow") && data["openTargetInNewWindow"] != null)
                        ((UrlField)field).OpenTargetInNewWindow = (bool?)data["openTargetInNewWindow"];
                    break;
                default:
                    {
                        FieldResponse response = new FieldResponse();
                        response.Timestamp = DateTime.UtcNow;
                        response.Success = false;
                        response.Message = "Not supported field type!";
                        response.Success = false;
                        return response;
                    }
            }

            field.Id = id.HasValue && id.Value != Guid.Empty ? id.Value : Guid.NewGuid();
            field.Name = name;
            field.Label = label;
            field.PlaceholderText = placeholderText;
            field.Description = description;
            field.HelpText = helpText;
            field.Required = required;
            field.Unique = unique;
            field.Searchable = searchable;
            field.Auditable = auditable;
            field.System = system;

            return CreateField(entityId, field.MapTo<InputField>());
        }

        public FieldResponse UpdateField(Guid entityId, InputField inputField)
        {
            FieldResponse response = new FieldResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();

            return UpdateField(entity, inputField);
        }

        public FieldResponse UpdateField(Entity entity, InputField inputField)
        {
            FieldResponse response = new FieldResponse
            {
                Success = true,
                Message = "The field was successfully updated!",
            };

            Field field = null;

            try
            {
                response.Errors = ValidateField(entity, inputField, true);

                field = inputField.MapTo<Field>();

                if (response.Errors.Count > 0)
                {
                    response.Object = field;
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The field was not updated. Validation error occurred!";
                    return response;
                }

                Field fieldForDelete = entity.Fields.FirstOrDefault(f => f.Id == field.Id);
                if (fieldForDelete.Id == field.Id)
                    entity.Fields.Remove(fieldForDelete);

                entity.Fields.Add(field);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The field was not updated! An internal error occurred!";
                    return response;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = field;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The field was not updated. An internal error occurred!";
#endif
                return response;
            }

            response.Object = field;
            response.Timestamp = DateTime.UtcNow;

            return response;
        }

        //		public FieldResponse PartialUpdateField(Guid entityId, Guid id, InputField inputField)
        //		{
        //			FieldResponse response = new FieldResponse
        //			{
        //				Success = true,
        //				Message = "The field was successfully updated!",
        //			};

        //			Field updatedField = null;

        //			try
        //			{
        //				IStorageEntity storageEntity = EntityRepository.Read(entityId);

        //				if (storageEntity == null)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "Entity with such Id does not exist!";
        //					return response;
        //				}

        //				Entity entity = storageEntity.MapTo<Entity>();

        //				updatedField = entity.Fields.FirstOrDefault(f => f.Id == id);

        //				if (updatedField == null)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "Field with such Id does not exist!";
        //					return response;
        //				}

        //				if (updatedField is AutoNumberField)
        //				{
        //					if (((InputAutoNumberField)inputField).DefaultValue != null)
        //						((AutoNumberField)updatedField).DefaultValue = ((InputAutoNumberField)inputField).DefaultValue;
        //					if (((InputAutoNumberField)inputField).DisplayFormat != null)
        //						((AutoNumberField)updatedField).DisplayFormat = ((InputAutoNumberField)inputField).DisplayFormat;
        //					if (((InputAutoNumberField)inputField).StartingNumber != null)
        //						((AutoNumberField)updatedField).StartingNumber = ((InputAutoNumberField)inputField).StartingNumber;
        //				}
        //				else if (updatedField is CheckboxField)
        //				{
        //					if (((InputCheckboxField)inputField).DefaultValue != null)
        //						((CheckboxField)updatedField).DefaultValue = ((InputCheckboxField)inputField).DefaultValue;
        //				}
        //				else if (updatedField is CurrencyField)
        //				{
        //					if (((InputCurrencyField)inputField).DefaultValue != null)
        //						((CurrencyField)updatedField).DefaultValue = ((InputCurrencyField)inputField).DefaultValue;
        //					if (((InputCurrencyField)inputField).MinValue != null)
        //						((CurrencyField)updatedField).MinValue = ((InputCurrencyField)inputField).MinValue;
        //					if (((InputCurrencyField)inputField).MaxValue != null)
        //						((CurrencyField)updatedField).MaxValue = ((InputCurrencyField)inputField).MaxValue;
        //					if (((InputCurrencyField)inputField).Currency != null)
        //						((CurrencyField)updatedField).Currency = ((InputCurrencyField)inputField).Currency;
        //				}
        //				else if (updatedField is DateField)
        //				{
        //					if (((InputDateField)inputField).DefaultValue != null)
        //						((DateField)updatedField).DefaultValue = ((InputDateField)inputField).DefaultValue;
        //					if (((InputDateField)inputField).Format != null)
        //						((DateField)updatedField).Format = ((InputDateField)inputField).Format;
        //					if (((InputDateField)inputField).UseCurrentTimeAsDefaultValue != null)
        //						((DateField)updatedField).UseCurrentTimeAsDefaultValue = ((InputDateField)inputField).UseCurrentTimeAsDefaultValue;
        //				}
        //				else if (updatedField is DateTimeField)
        //				{
        //					if (((InputDateTimeField)inputField).DefaultValue != null)
        //						((DateTimeField)updatedField).DefaultValue = ((InputDateTimeField)inputField).DefaultValue;
        //					if (((InputDateTimeField)inputField).Format != null)
        //						((DateTimeField)updatedField).Format = ((InputDateTimeField)inputField).Format;
        //					if (((InputDateTimeField)inputField).UseCurrentTimeAsDefaultValue != null)
        //						((DateTimeField)updatedField).UseCurrentTimeAsDefaultValue = ((InputDateTimeField)inputField).UseCurrentTimeAsDefaultValue;
        //				}
        //				else if (updatedField is EmailField)
        //				{
        //					if (((InputEmailField)inputField).DefaultValue != null)
        //						((EmailField)updatedField).DefaultValue = ((InputEmailField)inputField).DefaultValue;
        //					if (((InputEmailField)inputField).MaxLength != null)
        //						((EmailField)updatedField).MaxLength = ((InputEmailField)inputField).MaxLength;
        //				}
        //				else if (updatedField is FileField)
        //				{
        //					if (((InputFileField)inputField).DefaultValue != null)
        //						((FileField)updatedField).DefaultValue = ((InputFileField)inputField).DefaultValue;
        //				}
        //				else if (updatedField is HtmlField)
        //				{
        //					if (((InputHtmlField)inputField).DefaultValue != null)
        //						((HtmlField)updatedField).DefaultValue = ((InputHtmlField)inputField).DefaultValue;
        //				}
        //				else if (updatedField is ImageField)
        //				{
        //					if (((InputImageField)inputField).DefaultValue != null)
        //						((ImageField)updatedField).DefaultValue = ((InputImageField)inputField).DefaultValue;
        //				}
        //				else if (updatedField is MultiLineTextField)
        //				{
        //					if (((InputMultiLineTextField)inputField).DefaultValue != null)
        //						((MultiLineTextField)updatedField).DefaultValue = ((InputMultiLineTextField)inputField).DefaultValue;
        //					if (((InputMultiLineTextField)inputField).MaxLength != null)
        //						((MultiLineTextField)updatedField).MaxLength = ((InputMultiLineTextField)inputField).MaxLength;
        //					if (((InputMultiLineTextField)inputField).VisibleLineNumber != null)
        //						((MultiLineTextField)updatedField).VisibleLineNumber = ((InputMultiLineTextField)inputField).VisibleLineNumber;
        //				}
        //				else if (updatedField is MultiSelectField)
        //				{
        //					if (((InputMultiSelectField)inputField).DefaultValue != null)
        //						((MultiSelectField)updatedField).DefaultValue = ((InputMultiSelectField)inputField).DefaultValue;
        //					if (((InputMultiSelectField)inputField).Options != null)
        //						((MultiSelectField)updatedField).Options = ((InputMultiSelectField)inputField).Options;
        //				}
        //				else if (updatedField is NumberField)
        //				{
        //					if (((InputNumberField)inputField).DefaultValue != null)
        //						((NumberField)updatedField).DefaultValue = ((InputNumberField)inputField).DefaultValue;
        //					if (((InputNumberField)inputField).MinValue != null)
        //						((NumberField)updatedField).MinValue = ((InputNumberField)inputField).MinValue;
        //					if (((InputNumberField)inputField).MaxValue != null)
        //						((NumberField)updatedField).MaxValue = ((InputNumberField)inputField).MaxValue;
        //					if (((InputNumberField)inputField).DecimalPlaces != null)
        //						((NumberField)updatedField).DecimalPlaces = ((InputNumberField)inputField).DecimalPlaces;
        //				}
        //				else if (updatedField is PasswordField)
        //				{
        //					if (((InputPasswordField)inputField).MaxLength != null)
        //						((PasswordField)updatedField).MaxLength = ((InputPasswordField)inputField).MaxLength;
        //					if (((InputPasswordField)inputField).MinLength != null)
        //						((PasswordField)updatedField).MinLength = ((InputPasswordField)inputField).MinLength;
        //					if (((InputPasswordField)inputField).Encrypted != null)
        //						((PasswordField)updatedField).Encrypted = ((InputPasswordField)inputField).Encrypted;
        //				}
        //				else if (updatedField is PercentField)
        //				{
        //					if (((InputPercentField)inputField).DefaultValue != null)
        //						((PercentField)updatedField).DefaultValue = ((InputPercentField)inputField).DefaultValue;
        //					if (((InputPercentField)inputField).MinValue != null)
        //						((PercentField)updatedField).MinValue = ((InputPercentField)inputField).MinValue;
        //					if (((InputPercentField)inputField).MaxValue != null)
        //						((PercentField)updatedField).MaxValue = ((InputPercentField)inputField).MaxValue;
        //					if (((InputPercentField)inputField).DecimalPlaces != null)
        //						((PercentField)updatedField).DecimalPlaces = ((InputPercentField)inputField).DecimalPlaces;
        //				}
        //				else if (updatedField is PhoneField)
        //				{
        //					if (((InputPhoneField)inputField).DefaultValue != null)
        //						((PhoneField)updatedField).DefaultValue = ((InputPhoneField)inputField).DefaultValue;
        //					if (((InputPhoneField)inputField).Format != null)
        //						((PhoneField)updatedField).Format = ((InputPhoneField)inputField).Format;
        //					if (((InputPhoneField)inputField).MaxLength != null)
        //						((PhoneField)updatedField).MaxLength = ((InputPhoneField)inputField).MaxLength;
        //				}
        //				else if (updatedField is GuidField)
        //				{
        //					if (((InputGuidField)inputField).DefaultValue != null)
        //						((GuidField)updatedField).DefaultValue = ((InputGuidField)inputField).DefaultValue;
        //					if (((InputGuidField)inputField).GenerateNewId != null)
        //						((GuidField)updatedField).GenerateNewId = ((InputGuidField)inputField).GenerateNewId;
        //				}
        //				else if (updatedField is SelectField)
        //				{
        //					if (((InputSelectField)inputField).DefaultValue != null)
        //						((SelectField)updatedField).DefaultValue = ((InputSelectField)inputField).DefaultValue;
        //					if (((InputSelectField)inputField).Options != null)
        //						((SelectField)updatedField).Options = ((InputSelectField)inputField).Options;
        //				}
        //				else if (updatedField is TextField)
        //				{
        //					if (((InputTextField)inputField).DefaultValue != null)
        //						((TextField)updatedField).DefaultValue = ((InputTextField)inputField).DefaultValue;
        //					if (((InputTextField)inputField).MaxLength != null)
        //						((TextField)updatedField).MaxLength = ((InputTextField)inputField).MaxLength;
        //				}
        //				else if (updatedField is UrlField)
        //				{
        //					if (((InputUrlField)inputField).DefaultValue != null)
        //						((UrlField)updatedField).DefaultValue = ((InputUrlField)inputField).DefaultValue;
        //					if (((InputUrlField)inputField).MaxLength != null)
        //						((UrlField)updatedField).MaxLength = ((InputUrlField)inputField).MaxLength;
        //					if (((InputUrlField)inputField).OpenTargetInNewWindow != null)
        //						((UrlField)updatedField).OpenTargetInNewWindow = ((InputUrlField)inputField).OpenTargetInNewWindow;
        //				}

        //				if (inputField.Label != null)
        //					updatedField.Label = inputField.Label;
        //				else if (inputField.PlaceholderText != null)
        //					updatedField.PlaceholderText = inputField.PlaceholderText;
        //				else if (inputField.Description != null)
        //					updatedField.Description = inputField.Description;
        //				else if (inputField.HelpText != null)
        //					updatedField.HelpText = inputField.HelpText;
        //				else if (inputField.Required != null)
        //					updatedField.Required = inputField.Required.Value;
        //				else if (inputField.Unique != null)
        //					updatedField.Unique = inputField.Unique.Value;
        //				else if (inputField.Searchable != null)
        //					updatedField.Searchable = inputField.Searchable.Value;
        //				else if (inputField.Auditable != null)
        //					updatedField.Auditable = inputField.Auditable.Value;
        //				else if (inputField.System != null)
        //					updatedField.System = inputField.System.Value;

        //				response.Object = updatedField;
        //				response.Errors = ValidateField(entity, updatedField.MapTo<InputField>(), true);

        //				if (response.Errors.Count > 0)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The field was not updated. Validation error occurred!";
        //					return response;
        //				}

        //				IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
        //				bool result = EntityRepository.Update(updatedEntity);
        //				if (!result)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The field was not updated! An internal error occurred!";
        //					return response;
        //				}

        //			}
        //			catch (Exception e)
        //			{
        //				response.Success = false;
        //				response.Object = updatedField;
        //				response.Timestamp = DateTime.UtcNow;
        //#if DEBUG
        //				response.Message = e.Message + e.StackTrace;
        //#else
        //                response.Message = "The field was not updated. An internal error occurred!";
        //#endif
        //				return response;
        //			}

        //			response.Object = updatedField;
        //			response.Timestamp = DateTime.UtcNow;

        //			return response;
        //		}

        public FieldResponse DeleteField(Guid entityId, Guid id, bool transactional = true)
        {
            FieldResponse response = new FieldResponse
            {
                Success = true,
                Message = "The field was successfully deleted!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                Entity entity = storageEntity.MapTo<Entity>();

                Field field = entity.Fields.FirstOrDefault(f => f.Id == id);

                if (field == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The field was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("id", id.ToString(), "Field with such Id does not exist!"));
                    return response;
                }

                entity.Fields.Remove(field);

                var recRep = Storage.GetRecordRepository();
                var transaction = recRep.CreateTransaction();
                try
                {
                    if (transactional)
                        transaction.Begin();

                    recRep.RemoveRecordField(entity.Name, field.Name);

                    IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                    bool result = EntityRepository.Update(updatedEntity);
                    if (!result)
                    {
                        response.Timestamp = DateTime.UtcNow;
                        response.Success = false;
                        response.Message = "The field was not updated! An internal error occurred!";
                        return response;
                    }
                    if (transactional)
                        transaction.Commit();
                }
                catch
                {
                    if (transactional)
                        transaction.Rollback();
                    throw;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The field was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public FieldListResponse ReadFields(Guid entityId)
        {
            FieldListResponse response = new FieldListResponse
            {
                Success = true,
                Message = "The field was successfully returned!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                FieldList fieldList = new FieldList();
                fieldList.Fields = new List<Field>();

                foreach (IStorageField storageField in storageEntity.Fields)
                {
                    fieldList.Fields.Add(storageField.MapTo<Field>());
                }

                response.Object = fieldList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public FieldListResponse ReadFields()
        {
            FieldListResponse response = new FieldListResponse
            {
                Success = true,
                Message = "The field was successfully returned!",
            };

            try
            {
                List<IStorageEntity> storageEntities = EntityRepository.Read();

                FieldList fieldList = new FieldList();

                foreach (IStorageEntity entity in storageEntities)
                {
                    fieldList.Fields.AddRange(entity.Fields.MapTo<Field>());
                }

                response.Object = fieldList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public FieldResponse ReadField(Guid entityId, Guid id)
        {
            FieldResponse response = new FieldResponse
            {
                Success = true,
                Message = "The field was successfully returned!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                IStorageField storageField = storageEntity.Fields.FirstOrDefault(f => f.Id == id);

                if (storageField == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Validation error occurred!";
                    response.Errors.Add(new ErrorModel("id", id.ToString(), "Field with such Id does not exist!"));
                    return response;
                }

                Field field = storageField.MapTo<Field>();
                response.Object = field;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        #endregion

        #region << RecordsList methods >>

        public RecordListResponse CreateRecordList(Guid entityId, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return CreateRecordList(entity, inputRecordList);
        }

        public RecordListResponse CreateRecordList(string entityName, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityName);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return CreateRecordList(entity, inputRecordList);
        }

        private RecordListResponse CreateRecordList(Entity entity, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse
            {
                Success = true,
                Message = "The list was successfully created!",
            };


            if (!inputRecordList.Id.HasValue)
                inputRecordList.Id = Guid.NewGuid();

            RecordList recordList = inputRecordList.MapTo<RecordList>();

            try
            {
                response.Object = recordList;
                response.Errors = ValidateRecordList(entity, inputRecordList, false);

                recordList = inputRecordList.MapTo<RecordList>();

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not created. Validation error occurred!";
                    return response;
                }

                entity.RecordLists.Add(recordList);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not created! An internal error occurred!";
                    return response;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = recordList;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The list was not created. An internal error occurred!";
#endif
                return response;
            }

            return ReadRecordList(entity.Id, recordList.Id);
        }

        public RecordListResponse UpdateRecordList(Guid entityId, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return UpdateRecordList(entity, inputRecordList);
        }

        public RecordListResponse UpdateRecordList(string entityName, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityName);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return UpdateRecordList(entity, inputRecordList);
        }

        public RecordListResponse UpdateRecordList(Entity entity, InputRecordList inputRecordList)
        {
            RecordListResponse response = new RecordListResponse
            {
                Success = true,
                Message = "The list was successfully updated!",
            };

            RecordList recordList = inputRecordList.MapTo<RecordList>();

            try
            {
                response.Object = recordList;
                response.Errors = ValidateRecordList(entity, inputRecordList, true);

                recordList = inputRecordList.MapTo<RecordList>();

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not updated. Validation error occurred!";
                    return response;
                }

                RecordList listForDelete = entity.RecordLists.FirstOrDefault(r => r.Id == recordList.Id);
                if (listForDelete.Id == recordList.Id)
                    entity.RecordLists.Remove(listForDelete);

                entity.RecordLists.Add(recordList);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not updated! An internal error occurred!";
                    return response;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = recordList;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The list was not updated. An internal error occurred!";
#endif
                return response;
            }

            return ReadRecordList(entity.Id, recordList.Id);
        }

        //public RecordListResponse PartialUpdateRecordList(Guid entityId, Guid id, InputRecordList inputRecordList)
        //{
        //	RecordListResponse response = new RecordListResponse();

        //	IStorageEntity storageEntity = EntityRepository.Read(entityId);

        //	if (storageEntity == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "Entity with such Id does not exist!";
        //		return response;
        //	}

        //	Entity entity = storageEntity.MapTo<Entity>();

        //	RecordList updatedList = entity.RecordLists.FirstOrDefault(l => l.Id == id);

        //	if (updatedList == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "List with such Id does not exist!";
        //		return response;
        //	}

        //	return PartialUpdateRecordList(entity, updatedList, inputRecordList);
        //}

        //public RecordListResponse PartialUpdateRecordList(string entityName, string name, InputRecordList inputRecordList)
        //{
        //	RecordListResponse response = new RecordListResponse();

        //	IStorageEntity storageEntity = EntityRepository.Read(entityName);

        //	if (storageEntity == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "Entity with such Name does not exist!";
        //		return response;
        //	}

        //	Entity entity = storageEntity.MapTo<Entity>();

        //	RecordList updatedList = entity.RecordLists.FirstOrDefault(l => l.Name == name);

        //	if (updatedList == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "List with such Name does not exist!";
        //		return response;
        //	}

        //	return PartialUpdateRecordList(entity, updatedList, inputRecordList);
        //}

        //private RecordListResponse PartialUpdateRecordList(Entity entity, RecordList updatedList, InputRecordList inputRecordList)
        //		{
        //			RecordListResponse response = new RecordListResponse
        //			{
        //				Success = true,
        //				Message = "The list was successfully updated!",
        //			};

        //			try
        //			{
        //				if (inputRecordList.Label != null)
        //					updatedList.Label = inputRecordList.Label;
        //				if (inputRecordList.Default.HasValue)
        //					updatedList.Default = inputRecordList.Default;
        //				if (inputRecordList.System.HasValue)
        //					updatedList.System = inputRecordList.System;
        //				if (inputRecordList.Weight.HasValue)
        //					updatedList.Weight = inputRecordList.Weight;
        //				if (inputRecordList.CssClass != null)
        //					updatedList.CssClass = inputRecordList.CssClass;
        //				if (inputRecordList.Type != null)
        //					updatedList.Type = inputRecordList.Type;
        //				if (inputRecordList.PageSize.HasValue)
        //					updatedList.PageSize = inputRecordList.PageSize.Value;
        //				if (inputRecordList.Columns != null)
        //					updatedList.Columns = inputRecordList.Columns.MapTo<RecordListItemBase>();
        //				if (inputRecordList.Query != null)
        //					updatedList.Query = inputRecordList.Query.MapTo<RecordListQuery>();
        //				if (inputRecordList.Sorts != null)
        //					updatedList.Sorts = inputRecordList.Sorts.MapTo<RecordListSort>();

        //				response.Object = inputRecordList.MapTo<RecordList>();
        //				response.Errors = ValidateRecordList(entity, updatedList.MapTo<InputRecordList>(), true);

        //				if (response.Errors.Count > 0)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The list was not updated. Validation error occurred!";
        //					return response;
        //				}

        //				IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
        //				bool result = EntityRepository.Update(updatedEntity);
        //				if (!result)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The list was not updated! An internal error occurred!";
        //					return response;
        //				}

        //			}
        //			catch (Exception e)
        //			{
        //				response.Success = false;
        //				response.Object = inputRecordList.MapTo<RecordList>();
        //				response.Timestamp = DateTime.UtcNow;
        //#if DEBUG
        //				response.Message = e.Message + e.StackTrace;
        //#else
        //                response.Message = "The list was not updated. An internal error occurred!";
        //#endif
        //				return response;
        //			}

        //			return ReadRecordList(entity.Id, updatedList.Id);
        //		}

        public RecordListResponse DeleteRecordList(Guid entityId, Guid id)
        {
            RecordListResponse response = new RecordListResponse
            {
                Success = true,
                Message = "The list was successfully deleted!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                Entity entity = storageEntity.MapTo<Entity>();

                RecordList recordList = entity.RecordLists.FirstOrDefault(v => v.Id == id);

                if (recordList == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("id", id.ToString(), "List with such Id does not exist!"));
                    return response;
                }

                entity.RecordLists.Remove(recordList);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not updated! An internal error occurred!";
                    return response;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The list was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public RecordListResponse DeleteRecordList(string entityName, string name)
        {
            RecordListResponse response = new RecordListResponse
            {
                Success = true,
                Message = "The list was successfully deleted!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityName);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Name does not exist!";
                    return response;
                }

                Entity entity = storageEntity.MapTo<Entity>();

                RecordList recordList = entity.RecordLists.FirstOrDefault(l => l.Name == name);

                if (recordList == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("name", name, "List with such Name does not exist!"));
                    return response;
                }

                entity.RecordLists.Remove(recordList);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The list was not updated! An internal error occurred!";
                    return response;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The list was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public RecordListCollectionResponse ReadRecordLists(Guid entityId)
        {
            RecordListCollectionResponse response = new RecordListCollectionResponse();

            EntityResponse entityResponse = ReadEntity(entityId);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            return ReadRecordLists(entity);
        }

        public RecordListCollectionResponse ReadRecordLists(string entityName)
        {
            RecordListCollectionResponse response = new RecordListCollectionResponse();

            EntityResponse entityResponse = ReadEntity(entityName);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            return ReadRecordLists(entity);
        }

        public RecordListCollectionResponse ReadRecordLists(Entity entity)
        {
            RecordListCollectionResponse response = new RecordListCollectionResponse
            {
                Success = true,
                Message = "The lists were successfully returned!",
            };

            try
            {
                RecordListCollection recordListCollection = new RecordListCollection();
                recordListCollection.RecordLists = entity.RecordLists;

                response.Object = recordListCollection;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public RecordListResponse ReadRecordList(Guid entityId, Guid id)
        {
            RecordListResponse response = new RecordListResponse();

            EntityResponse entityResponse = ReadEntity(entityId);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            RecordList recordList = entity.RecordLists.FirstOrDefault(r => r.Id == id);

            if (recordList == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Record List with such Id does not exist!";
                return response;
            }


            return ReadRecordList(entity, recordList);
        }

        public RecordListResponse ReadRecordList(string entityName, string name)
        {
            RecordListResponse response = new RecordListResponse();

            EntityResponse entityResponse = ReadEntity(entityName);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            RecordList recordList = entity.RecordLists.FirstOrDefault(r => r.Name == name);

            if (recordList == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Record List with such Name does not exist!";
                return response;
            }

            return ReadRecordList(entity, recordList);
        }

        private RecordListResponse ReadRecordList(Entity entity, RecordList recordList)
        {
            RecordListResponse response = new RecordListResponse
            {
                Success = true,
                Message = "The list was successfully returned!",
            };

            try
            {
                response.Object = recordList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public RecordListCollectionResponse ReadRecordLists()
        {
            RecordListCollectionResponse response = new RecordListCollectionResponse
            {
                Success = true,
                Message = "The lists were successfully returned!",
            };

            try
            {
                List<IStorageEntity> storageEntities = EntityRepository.Read();

                RecordListCollection recordListCollection = new RecordListCollection();
                recordListCollection.RecordLists = new List<RecordList>();

                foreach (IStorageEntity entity in storageEntities)
                {
                    recordListCollection.RecordLists.AddRange(entity.RecordLists.MapTo<RecordList>());
                }

                response.Object = recordListCollection;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        #endregion

        #region << RecordView methods >>

        public RecordViewResponse CreateRecordView(Guid entityId, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return CreateRecordView(entity, inputRecordView);
        }

        public RecordViewResponse CreateRecordView(string entityName, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityName);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return CreateRecordView(entity, inputRecordView);
        }

        private RecordViewResponse CreateRecordView(Entity entity, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse
            {
                Success = true,
                Message = "The record view was successfully created!",
            };

            if (!inputRecordView.Id.HasValue)
                inputRecordView.Id = Guid.NewGuid();

            RecordView recordView = inputRecordView.MapTo<RecordView>();

            try
            {
                response.Object = recordView;
                response.Errors = ValidateRecordView(entity, inputRecordView, false);

                recordView = inputRecordView.MapTo<RecordView>();

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not created. Validation error occurred!";
                    return response;
                }

                entity.RecordViews.Add(recordView);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not created! An internal error occurred!";
                    return response;
                }
            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = recordView;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The record view was not created. An internal error occurred!";
#endif
                return response;
            }

            return ReadRecordView(entity.Id, recordView.Id);
        }

        public RecordViewResponse UpdateRecordView(Guid entityId, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityId);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return UpdateRecordView(entity, inputRecordView);
        }

        public RecordViewResponse UpdateRecordView(string entityName, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse();

            IStorageEntity storageEntity = EntityRepository.Read(entityName);

            if (storageEntity == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = storageEntity.MapTo<Entity>();


            return UpdateRecordView(entity, inputRecordView);
        }

        public RecordViewResponse UpdateRecordView(Entity entity, InputRecordView inputRecordView)
        {
            RecordViewResponse response = new RecordViewResponse
            {
                Success = true,
                Message = "The record view was successfully updated!",
            };

            RecordView recordView = inputRecordView.MapTo<RecordView>();

            try
            {
                response.Object = recordView;
                response.Errors = ValidateRecordView(entity, inputRecordView, true);

                recordView = inputRecordView.MapTo<RecordView>();

                if (response.Errors.Count > 0)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not updated. Validation error occurred!";
                    return response;
                }

                RecordView recordViewForDelete = entity.RecordViews.FirstOrDefault(r => r.Id == recordView.Id);
                if (recordViewForDelete.Id == recordView.Id)
                    entity.RecordViews.Remove(recordViewForDelete);

                entity.RecordViews.Add(recordView);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not updated! An internal error occurred!";
                    return response;
                }

            }
            catch (Exception e)
            {
                response.Success = false;
                response.Object = recordView;
                response.Timestamp = DateTime.UtcNow;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The record view was not updated. An internal error occurred!";
#endif
                return response;
            }

            return ReadRecordView(entity.Id, recordView.Id);
        }

        //public RecordViewResponse PartialUpdateRecordView(Guid entityId, Guid id, InputRecordView inputRecordView)
        //{
        //	RecordViewResponse response = new RecordViewResponse();

        //	IStorageEntity storageEntity = EntityRepository.Read(entityId);

        //	if (storageEntity == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "Entity with such Id does not exist!";
        //		return response;
        //	}

        //	Entity entity = storageEntity.MapTo<Entity>();

        //	RecordView updatedView = entity.RecordViews.FirstOrDefault(v => v.Id == id);

        //	if (updatedView == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "View with such Id does not exist!";
        //		return response;
        //	}

        //	return PartialUpdateRecordView(entity, updatedView, inputRecordView);
        //}

        //public RecordViewResponse PartialUpdateRecordView(string entityName, string name, InputRecordView inputRecordView)
        //{
        //	RecordViewResponse response = new RecordViewResponse();

        //	IStorageEntity storageEntity = EntityRepository.Read(entityName);

        //	if (storageEntity == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "Entity with such Name does not exist!";
        //		return response;
        //	}

        //	Entity entity = storageEntity.MapTo<Entity>();

        //	RecordView updatedView = entity.RecordViews.FirstOrDefault(v => v.Name == name);

        //	if (updatedView == null)
        //	{
        //		response.Timestamp = DateTime.UtcNow;
        //		response.Success = false;
        //		response.Message = "View with such Name does not exist!";
        //		return response;
        //	}

        //	return PartialUpdateRecordView(entity, updatedView, inputRecordView);
        //}

        //private RecordViewResponse PartialUpdateRecordView(Entity entity, RecordView updatedView, InputRecordView inputRecordView)
        //		{
        //			RecordViewResponse response = new RecordViewResponse
        //			{
        //				Success = true,
        //				Message = "The record view was successfully updated!",
        //			};

        //			RecordView recordView = inputRecordView.MapTo<RecordView>();

        //			try
        //			{
        //				if (inputRecordView.Label != null)
        //					updatedView.Label = inputRecordView.Label;
        //				if (inputRecordView.Default.HasValue)
        //					updatedView.Default = inputRecordView.Default;
        //				if (inputRecordView.System.HasValue)
        //					updatedView.System = inputRecordView.System;
        //				if (inputRecordView.Weight.HasValue)
        //					updatedView.Weight = inputRecordView.Weight;
        //				if (inputRecordView.CssClass != null)
        //					updatedView.CssClass = inputRecordView.CssClass;
        //				if (!string.IsNullOrEmpty(inputRecordView.Type))
        //					updatedView.Type = inputRecordView.Type;
        //				if (inputRecordView.Regions != null)
        //					updatedView.Regions = inputRecordView.Regions.MapTo<RecordViewRegion>();
        //				if (inputRecordView.Sidebar != null)
        //					updatedView.Sidebar = inputRecordView.Sidebar.MapTo<RecordViewSidebar>();

        //				response.Object = recordView;
        //				response.Errors = ValidateRecordView(entity, updatedView.MapTo<InputRecordView>(), true);

        //				if (response.Errors.Count > 0)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The record view was not updated. Validation error occurred!";
        //					return response;
        //				}

        //				IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
        //				bool result = EntityRepository.Update(updatedEntity);
        //				if (!result)
        //				{
        //					response.Timestamp = DateTime.UtcNow;
        //					response.Success = false;
        //					response.Message = "The record view was not updated! An internal error occurred!";
        //					return response;
        //				}

        //			}
        //			catch (Exception e)
        //			{
        //				response.Success = false;
        //				response.Object = recordView;
        //				response.Timestamp = DateTime.UtcNow;
        //#if DEBUG
        //				response.Message = e.Message + e.StackTrace;
        //#else
        //                response.Message = "The record view was not updated. An internal error occurred!";
        //#endif
        //				return response;
        //			}

        //			return ReadRecordView(entity.Id, recordView.Id);
        //		}

        public RecordViewResponse DeleteRecordView(Guid entityId, Guid id)
        {
            RecordViewResponse response = new RecordViewResponse
            {
                Success = true,
                Message = "The record view was successfully deleted!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityId);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Id does not exist!";
                    return response;
                }

                Entity entity = storageEntity.MapTo<Entity>();

                RecordView recordView = entity.RecordViews.FirstOrDefault(r => r.Id == id);

                if (recordView == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("id", id.ToString(), "Record view with such Id does not exist!"));
                    return response;
                }

                entity.RecordViews.Remove(recordView);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not updated! An internal error occurred!";
                    return response;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The record view was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public RecordViewResponse DeleteRecordView(string entityName, string name)
        {
            RecordViewResponse response = new RecordViewResponse
            {
                Success = true,
                Message = "The record view was successfully deleted!",
            };

            try
            {
                IStorageEntity storageEntity = EntityRepository.Read(entityName);

                if (storageEntity == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "Entity with such Name does not exist!";
                    return response;
                }

                Entity entity = storageEntity.MapTo<Entity>();

                RecordView recordView = entity.RecordViews.FirstOrDefault(r => r.Name == name);

                if (recordView == null)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not deleted. Validation error occurred!";
                    response.Errors.Add(new ErrorModel("name", name, "Record view with such Name does not exist!"));
                    return response;
                }

                entity.RecordViews.Remove(recordView);

                IStorageEntity updatedEntity = entity.MapTo<IStorageEntity>();
                bool result = EntityRepository.Update(updatedEntity);
                if (!result)
                {
                    response.Timestamp = DateTime.UtcNow;
                    response.Success = false;
                    response.Message = "The record view was not updated! An internal error occurred!";
                    return response;
                }
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "The record view was not deleted. An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.UtcNow;
            return response;
        }

        public RecordViewCollectionResponse ReadRecordViews(Guid entityId)
        {
            RecordViewCollectionResponse response = new RecordViewCollectionResponse();

            EntityResponse entityResponse = ReadEntity(entityId);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            return ReadRecordViews(entity);
        }

        public RecordViewCollectionResponse ReadRecordViews(string entityName)
        {
            RecordViewCollectionResponse response = new RecordViewCollectionResponse();

            EntityResponse entityResponse = ReadEntity(entityName);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;


            return ReadRecordViews(entity);
        }

        private RecordViewCollectionResponse ReadRecordViews(Entity entity)
        {
            RecordViewCollectionResponse response = new RecordViewCollectionResponse
            {
                Success = true,
                Message = "The record views were successfully returned!",
            };

            try
            {
                RecordViewCollection recordViewList = new RecordViewCollection();
                recordViewList.RecordViews = entity.RecordViews;

                response.Object = recordViewList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public RecordViewResponse ReadRecordView(Guid entityId, Guid id)
        {
            RecordViewResponse response = new RecordViewResponse();

            EntityResponse entityResponse = ReadEntity(entityId);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such Id does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            RecordView recordView = entity.RecordViews.FirstOrDefault(r => r.Id == id);

            if (recordView == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Record View with such Id does not exist!";
                return response;
            }


            return ReadRecordView(entity, recordView);
        }

        public RecordViewResponse ReadRecordView(string entityName, string name)
        {
            RecordViewResponse response = new RecordViewResponse();

            EntityResponse entityResponse = ReadEntity(entityName);

            if (!entityResponse.Success)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = entityResponse.Message;
                return response;
            }
            else if (entityResponse.Object == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Entity with such name does not exist!";
                return response;
            }

            Entity entity = entityResponse.Object;

            RecordView recordView = entity.RecordViews.FirstOrDefault(r => r.Name == name);

            if (recordView == null)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
                response.Message = "Record View with such Name does not exist!";
                return response;
            }

            return ReadRecordView(entity, recordView);
        }

        private RecordViewResponse ReadRecordView(Entity entity, RecordView recordView)
        {
            RecordViewResponse response = new RecordViewResponse
            {
                Success = true,
                Message = "The record view was successfully returned!",
            };

            try
            {
                response.Object = recordView;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        public RecordViewCollectionResponse ReadRecordViews()
        {
            RecordViewCollectionResponse response = new RecordViewCollectionResponse
            {
                Success = true,
                Message = "The record views were successfully returned!",
            };

            try
            {
                List<IStorageEntity> storageEntities = EntityRepository.Read();

                RecordViewCollection recordViewList = new RecordViewCollection();
                recordViewList.RecordViews = new List<RecordView>();

                foreach (IStorageEntity entity in storageEntities)
                {
                    recordViewList.RecordViews.AddRange(entity.RecordViews.MapTo<RecordView>());
                }

                response.Object = recordViewList;
            }
            catch (Exception e)
            {
                response.Timestamp = DateTime.UtcNow;
                response.Success = false;
#if DEBUG
                response.Message = e.Message + e.StackTrace;
#else
                response.Message = "An internal error occurred!";
#endif
                return response;
            }

            response.Timestamp = DateTime.Now;

            return response;
        }

        #endregion

        #region << Help methods >>

        private List<Field> CreateEntityDefaultFields(Entity entity)
        {
            List<Field> fields = new List<Field>();

            GuidField primaryKeyField = new GuidField();

            primaryKeyField.Id = Guid.NewGuid();
            primaryKeyField.Name = "id";
            primaryKeyField.Label = "Id";
            primaryKeyField.PlaceholderText = "";
            primaryKeyField.Description = "";
            primaryKeyField.HelpText = "";
            primaryKeyField.Required = true;
            primaryKeyField.Unique = true;
            primaryKeyField.Searchable = false;
            primaryKeyField.Auditable = false;
            primaryKeyField.System = true;
            primaryKeyField.DefaultValue = Guid.Empty;
            primaryKeyField.GenerateNewId = true;

            fields.Add(primaryKeyField);

            GuidField createdBy = new GuidField();

            createdBy.Id = Guid.NewGuid();
            createdBy.Name = "created_by";
            createdBy.Label = "Created By";
            createdBy.PlaceholderText = "";
            createdBy.Description = "";
            createdBy.HelpText = "";
            createdBy.Required = false;
            createdBy.Unique = false;
            createdBy.Searchable = false;
            createdBy.Auditable = false;
            createdBy.System = true;
            createdBy.DefaultValue = Guid.Empty;
            createdBy.GenerateNewId = false;

            fields.Add(createdBy);

            GuidField lastModifiedBy = new GuidField();

            lastModifiedBy.Id = Guid.NewGuid();
            lastModifiedBy.Name = "last_modified_by";
            lastModifiedBy.Label = "Last Modified By";
            lastModifiedBy.PlaceholderText = "";
            lastModifiedBy.Description = "";
            lastModifiedBy.HelpText = "";
            lastModifiedBy.Required = false;
            lastModifiedBy.Unique = false;
            lastModifiedBy.Searchable = false;
            lastModifiedBy.Auditable = false;
            lastModifiedBy.System = true;
            lastModifiedBy.DefaultValue = Guid.Empty;
            lastModifiedBy.GenerateNewId = false;

            fields.Add(lastModifiedBy);

            DateTimeField createdOn = new DateTimeField();

            createdOn.Id = Guid.NewGuid();
            createdOn.Name = "created_on";
            createdOn.Label = "Created On";
            createdOn.PlaceholderText = "";
            createdOn.Description = "";
            createdOn.HelpText = "";
            createdOn.Required = false;
            createdOn.Unique = false;
            createdOn.Searchable = false;
            createdOn.Auditable = false;
            createdOn.System = true;
            createdOn.DefaultValue = null;

            createdOn.Format = "MM/dd/YYYY";
            createdOn.UseCurrentTimeAsDefaultValue = true;

            fields.Add(createdOn);

            DateTimeField modifiedOn = new DateTimeField();

            modifiedOn.Id = Guid.NewGuid();
            modifiedOn.Name = "last_modified_on";
            modifiedOn.Label = "Last Modified On";
            modifiedOn.PlaceholderText = "";
            modifiedOn.Description = "";
            modifiedOn.HelpText = "";
            modifiedOn.Required = false;
            modifiedOn.Unique = false;
            modifiedOn.Searchable = false;
            modifiedOn.Auditable = false;
            modifiedOn.System = true;
            modifiedOn.DefaultValue = null;

            modifiedOn.Format = "MM/dd/YYYY";
            modifiedOn.UseCurrentTimeAsDefaultValue = true;

            fields.Add(modifiedOn);

            return fields;
        }

        private List<RecordList> CreateEntityDefaultRecordLists(Entity entity)
        {
            List<RecordList> recordLists = new List<RecordList>();


            return recordLists;
        }

        private List<RecordView> CreateEntityDefaultRecordViews(Entity entity)
        {
            List<RecordView> recordViewList = new List<RecordView>();


            return recordViewList;
        }

        public static EntityRecord ConvertToEntityRecord(object inputRecord)
        {
            EntityRecord record = new EntityRecord();

            foreach (var prop in inputRecord.GetType().GetProperties())
            {
                record[prop.Name] = prop.GetValue(inputRecord);
            }

            return record;
        }

        private static bool HasKey(Expando expando, string key)
        {
            return expando.GetProperties().Any(p => p.Key == key);
        }

        private Entity GetEntityByListId(Guid listId)
        {
            List<IStorageEntity> storageEntityList = EntityRepository.Read();
            List<Entity> entities = storageEntityList.MapTo<Entity>();

            return GetEntityByListId(listId, entities);
        }

        private static Entity GetEntityByListId(Guid listId, List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                if (entity.RecordLists.Any(l => l.Id == listId))
                    return entity;
            }

            return null;
        }

        private Entity GetEntityByViewId(Guid viewId)
        {
            List<IStorageEntity> storageEntityList = EntityRepository.Read();
            List<Entity> entities = storageEntityList.MapTo<Entity>();

            return GetEntityByViewId(viewId, entities);
        }

        private static Entity GetEntityByViewId(Guid viewId, List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                if (entity.RecordViews.Any(v => v.Id == viewId))
                    return entity;
            }

            return null;
        }

        private Entity GetEntityByFieldId(Guid fieldId)
        {
            List<IStorageEntity> storageEntityList = EntityRepository.Read();
            List<Entity> entities = storageEntityList.MapTo<Entity>();

            return GetEntityByFieldId(fieldId, entities);
        }

        private static Entity GetEntityByFieldId(Guid fieldId, List<Entity> entities)
        {
            foreach (var entity in entities)
            {
                if (entity.Fields.Any(v => v.Id == fieldId))
                    return entity;
            }

            return null;
        }

        #endregion
    }
}